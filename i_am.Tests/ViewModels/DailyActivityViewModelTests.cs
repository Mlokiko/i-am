using i_am.Models;
using i_am.Services;
using i_am.ViewModels;
using Moq;
using Xunit;

namespace i_am.Tests.ViewModels;

public class DailyActivityViewModelTests
{
    // 1. Arrange (Przygotowanie środowiska testowego)
    // 2. Act (Wykonanie akcji)
    // 3. Assert (Sprawdzenie wyniku)

    [Fact]
    public async Task InitializeAsync_WhenAlreadySubmitted_SetsHasAlreadySubmittedToTrue()
    {
        // --- ARRANGE ---
        var mockFirestore = new Mock<IFirestoreService>();

        // Uczymy fałszywy serwis, co ma odpowiadać
        mockFirestore.Setup(x => x.GetCurrentUserId())
                     .Returns("user_123");

        mockFirestore.Setup(x => x.GetReportingDateString())
                     .Returns("2023-10-27");

        // Symulujemy, że baza danych mówi: "Tak, raport został dziś wysłany"
        mockFirestore.Setup(x => x.HasSubmittedDailyResponseAsync("user_123", "2023-10-27"))
                     .ReturnsAsync(true);

        // Tworzymy ViewModel, wstrzykując mu nasz fałszywy serwis
        var viewModel = new DailyActivityViewModel(mockFirestore.Object);

        // --- ACT ---
        await viewModel.InitializeAsync();

        // --- ASSERT ---
        Assert.True(viewModel.HasAlreadySubmitted);
        Assert.False(viewModel.IsLoading); // Po załadowaniu Loading powinien być false
        Assert.Empty(viewModel.FormItems); // Skoro raport wysłany, lista pytań powinna być pusta
    }

    [Fact]
    public async Task InitializeAsync_WhenNotSubmitted_LoadsQuestions()
    {
        // --- ARRANGE ---
        var mockFirestore = new Mock<IFirestoreService>();
        mockFirestore.Setup(x => x.GetCurrentUserId()).Returns("user_123");
        mockFirestore.Setup(x => x.GetReportingDateString()).Returns("2023-10-27");
        mockFirestore.Setup(x => x.HasSubmittedDailyResponseAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        // Symulujemy pulę pytań pobraną z bazy
        var fakeQuestions = new List<QuestionTemplate>
        {
            new QuestionTemplate { Id = "q1", Text = "Jak się czujesz?", Type = "Closed", IsRandomPool = false, OrderIndex = 0 }
        };
        mockFirestore.Setup(x => x.GetQuestionTemplatesAsync("user_123")).ReturnsAsync(fakeQuestions);

        var viewModel = new DailyActivityViewModel(mockFirestore.Object);

        // --- ACT ---
        await viewModel.InitializeAsync();

        // --- ASSERT ---
        Assert.False(viewModel.HasAlreadySubmitted);
        Assert.Single(viewModel.FormItems); // Oczekujemy, że załadowało się dokładnie 1 pytanie
        Assert.Equal("Jak się czujesz?", viewModel.FormItems[0].Question.Text);
    }
}