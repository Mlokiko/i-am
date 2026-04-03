using Plugin.Firebase.Firestore;

namespace i_am.Models
{
    public class QuestionTemplate : IFirestoreObject
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("text")]
        public string Text { get; set; } = string.Empty;

        [FirestoreProperty("type")]
        public string Type { get; set; } = "Closed";

        // NOWE: Czy pytanie należy do puli losowanej?
        [FirestoreProperty("isRandomPool")]
        public bool IsRandomPool { get; set; }

        // NOWE: Ile maksymalnie odpowiedzi można zaznaczyć? (Domyślnie 1)
        [FirestoreProperty("maxSelections")]
        public int MaxSelections { get; set; } = 1;

        [FirestoreProperty("options")]
        public List<QuestionOption> Options { get; set; } = new();

        [FirestoreProperty("orderIndex")]
        public int OrderIndex { get; set; }
    }

    public class QuestionOption : IFirestoreObject
    {
        [FirestoreProperty("text")]
        public string Text { get; set; } = string.Empty;

        [FirestoreProperty("points")]
        public int Points { get; set; }
    }
}