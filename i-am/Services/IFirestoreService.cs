using i_am.Models;

namespace i_am.Services
{
    public interface IFirestoreService
    {
        #region User Management

        Task<string> RegisterAsync(string email, string password);

        Task<string> LoginAsync(string email, string password);

        Task SignOutAsync();

        bool IsUserLoggedIn();

        string? GetCurrentUserId();

        Task CreateUserProfileAsync(string uid, User profile);

        Task<User?> GetUserProfileAsync(string uid);

        Task DeleteAccountAndProfileAsync();

        Task UpdateFcmTokenAsync();

        #endregion

        #region Notifications

        Task SendNotificationAsync(AppNotification notification);

        IDisposable ListenForNotifications(
            string myUid,
            Action<List<AppNotification>> onUpdate);

        Task DeleteNotificationAsync(string notificationId);

        #endregion

        #region Invitations

        Task<bool> SendInvitationAsync(
            string senderId,
            string senderName,
            bool isSenderCaregiver,
            string receiverEmail);

        IDisposable ListenForReceivedInvitations(
            string myUid,
            Action<List<Invitation>> onUpdate);

        IDisposable ListenForSentInvitations(
            string myUid,
            Action<List<Invitation>> onUpdate);

        Task AcceptInvitationAsync(Invitation request);

        Task RejectInvitationAsync(Invitation invitation);

        Task DeleteInvitationAsync(string invitationId);

        Task RemoveAcceptedInvitationAsync(
            string caregiverId,
            string caretakerId,
            string removerUid,
            string removerName);

        Task DeleteInvitationPermanentlyAsync(string invitationId);

        Task<List<User>> GetUsersByIdsAsync(List<string>? userIds);

        #endregion

        #region Questions & Answers

        string GetReportingDateString();

        Task<List<QuestionTemplate>> GetQuestionTemplatesAsync(string careTakerId);

        Task SaveQuestionTemplateAsync(
            string careTakerId,
            QuestionTemplate template);

        Task DeleteQuestionTemplateAsync(
            string careTakerId,
            string templateId);

        Task<bool> HasSubmittedDailyResponseAsync(
            string careTakerId,
            string dateString);

        Task SaveDailyResponseAsync(
            string careTakerId,
            DailyResponse response);

        Task<List<QuestionTemplate>> InitializeDefaultQuestionsAsync(
            string careTakerId);

        Task<List<DailyResponse>> GetAllDailyResponsesAsync(
            string careTakerId);

        #endregion
    }
}
