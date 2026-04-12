const { onDocumentCreated } = require("firebase-functions/v2/firestore");
const { setGlobalOptions } = require("firebase-functions/v2");
const admin = require("firebase-admin");

admin.initializeApp();

setGlobalOptions({ region: "europe-central2" });

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