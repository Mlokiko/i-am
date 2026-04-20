const { onDocumentCreated } = require("firebase-functions/v2/firestore");
const { onSchedule } = require("firebase-functions/v2/scheduler");
const { setGlobalOptions } = require("firebase-functions/v2");
const admin = require("firebase-admin");

admin.initializeApp();

setGlobalOptions({ region: "europe-central2" });

// ----------------------------------------------------------------------
// 1. TWOJA OBECNA FUNKCJA WYSYŁAJĄCA PUSH
// ----------------------------------------------------------------------
exports.sendPushNotification = onDocumentCreated("notifications/{notificationId}", async (event) => {
  // W Gen 2 dane wyciągamy z event.data
  const snap = event.data;
  if (!snap) return;

  const notificationData = snap.data();
  const receiverId = notificationData.receiverId;

  // 1. Pobranie danych użytkownika
  const userDoc = await admin.firestore()
    .collection("users")
    .doc(receiverId)
    .get();

  if (!userDoc.exists) {
    return console.log("User nie istnieje");
  }

  const userData = userDoc.data();
  const fcmToken = userData.fcmToken;

  // 2. Wysłanie powiadomienia, jeśli użytkownik ma token
  if (fcmToken) {
    const message = {
      token: fcmToken,
      notification: {
        title: notificationData.title,
        body: notificationData.message,
      },
      // Zabezpieczenie przed wartościami undefined (FCM tego nie lubi)
      data: {
        type: notificationData.type || "System",
        notificationId: event.params.notificationId || "0",
      },
    };

    try {
      await admin.messaging().send(message);
      console.log(`Push wyslany do ${userData.name}`);
    } catch (error) {
      console.error("Blad podczas wysylania FCM:", error);
    }
  } else {
    console.log(`Brak tokena dla ${userData.name}`);
  }
});

// ----------------------------------------------------------------------
// 2. NOWA FUNKCJA HARMONOGRAMU - Sprawdzanie nieaktywności
// ----------------------------------------------------------------------
// Uruchamia się co godzinę (możesz zmienić np. na 'every 30 minutes')
exports.checkInactivity = onSchedule("every 1 hours", async (event) => {
  const db = admin.firestore();
  const now = Date.now();

  // Pobieramy wszystkich użytkowników, którzy są podopiecznymi
  const caretakersSnap = await db.collection("users")
    .where("isCareGiver", "==", false)
    .get();

  for (const doc of caretakersSnap.docs) {
    const caretaker = doc.data();
    // Jeśli nie ma przypisanych opiekunów, przeskakujemy
    if (!caretaker.careGiversID || caretaker.careGiversID.length === 0) continue;

    // Pobieramy ostatnią aktywność podopiecznego (jeśli brak danych, uznajemy że jest aktywny teraz)
    const lastActiveTime = caretaker.lastActiveAt 
        ? caretaker.lastActiveAt.toDate().getTime() 
        : now;
    
    const hoursInactive = (now - lastActiveTime) / (1000 * 60 * 60);

    for (const giverId of caretaker.careGiversID) {
      // Pobieramy profil konkretnego opiekuna
      const giverDoc = await db.collection("users").doc(giverId).get();
      if (!giverDoc.exists) continue;

      const giver = giverDoc.data();

      // Sprawdzamy ustawienia opiekuna odnośnie inaktywności
      if (!giver.inactivityAlertsEnabled) continue;

      const threshold = giver.inactivityThresholdHours || 24; // Default 24h

      if (hoursInactive >= threshold) {
        // ZABEZPIECZENIE PRZED SPAMEM:
        // Sprawdzamy, czy w ciągu ostatnich np. 12 godzin nie wysłaliśmy już takiego powiadomienia
        const recentAlertsSnap = await db.collection("notifications")
          .where("receiverId", "==", giverId)
          .where("senderId", "==", doc.id)
          .where("type", "==", "InactivityAlert")
          .orderBy("createdAt", "desc")
          .limit(1)
          .get();

        let shouldSend = true;
        if (!recentAlertsSnap.empty) {
          const lastAlertDoc = recentAlertsSnap.docs[0].data();
          const lastAlertTime = lastAlertDoc.createdAt ? lastAlertDoc.createdAt.toDate().getTime() : 0;
          const hoursSinceLastAlert = (now - lastAlertTime) / (1000 * 60 * 60);
          
          // Jeśli od ostatniego alarmu minęło mniej niż 12 godzin, nie spamujemy kolejnym
          if (hoursSinceLastAlert < 12) {
            shouldSend = false;
          }
        }

        // Jeśli osiągnął próg inaktywności i nie wysłaliśmy ostatnio alarmu - generujemy powiadomienie
        if (shouldSend) {
          await db.collection("notifications").add({
            receiverId: giverId,
            senderId: doc.id, // ID Podopiecznego, przydatne do wyświetlenia avatara itp.
            title: "Brak aktywności",
            message: `Podopieczny ${caretaker.name || 'Nieznany'} nie logował się od ponad ${Math.floor(hoursInactive)} godzin.`,
            type: "InactivityAlert",
            isRead: false,
            createdAt: admin.firestore.FieldValue.serverTimestamp()
          });
          console.log(`Wygenerowano alarm braku aktywności dla podopiecznego ${doc.id} (do opiekuna ${giverId})`);
        }
      }
    }
  }
});