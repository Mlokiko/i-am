using Plugin.Firebase.Firestore;

namespace i_am.Models
{
    public class DailyResponse : IFirestoreObject
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = string.Empty; // Format: "yyyy-MM-dd"

        [FirestoreProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [FirestoreProperty("totalScore")]
        public int TotalScore { get; set; }

        [FirestoreProperty("evaluationStatus")]
        public string EvaluationStatus { get; set; } = string.Empty;

        [FirestoreProperty("answers")]
        public List<GivenAnswer> Answers { get; set; } = new();

        [FirestoreProperty("frontPhotoUrl")]
        public string FrontPhotoUrl { get; set; } = string.Empty;

        [FirestoreProperty("rearPhotoUrl")]
        public string RearPhotoUrl { get; set; } = string.Empty;
    }

    public class GivenAnswer : IFirestoreObject
    {
        [FirestoreProperty("questionId")]
        public string QuestionId { get; set; } = string.Empty;

        [FirestoreProperty("questionText")]
        public string QuestionText { get; set; } = string.Empty;

        [FirestoreProperty("selectedOptionText")]
        public string SelectedOptionText { get; set; } = string.Empty;

        [FirestoreProperty("openTextResponse")]
        public string OpenTextResponse { get; set; } = string.Empty;

        [FirestoreProperty("pointsAwarded")]
        public int PointsAwarded { get; set; }

        public bool IsVisibleToCareGiver;
    }
}