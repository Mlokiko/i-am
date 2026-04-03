using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class EditCareTakerQuestionsViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private QuestionTemplate? _editingTemplate;

        // --- GŁÓWNE LISTY ---
        public ObservableCollection<User> CareTakers { get; } = new();
        public ObservableCollection<QuestionTemplate> Questions { get; } = new();

        [ObservableProperty]
        private User? selectedCareTaker;

        [ObservableProperty]
        private bool isQuestionsVisible;

        // --- PANEL EDYTORA ---
        [ObservableProperty] private bool isEditorVisible;
        [ObservableProperty] private string editorTitle = string.Empty;
        [ObservableProperty] private string editorQuestionText = string.Empty;
        [ObservableProperty] private string editorQuestionType = "Zamknięte"; // "Zamknięte" lub "Otwarte"

        public ObservableCollection<string> AvailableTypes { get; } = new(new[] { "Zamknięte", "Otwarte" });
        public ObservableCollection<QuestionOption> EditorOptions { get; } = new();

        public EditCareTakerQuestionsViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task InitializeAsync()
        {
            string? myUid = _firestoreService.GetCurrentUserId();
            if (string.IsNullOrEmpty(myUid)) return;

            var profile = await _firestoreService.GetUserProfileAsync(myUid);
            if (profile != null)
            {
                var careTakers = await _firestoreService.GetUsersByIdsAsync(profile.CaretakersID);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CareTakers.Clear();
                    foreach (var ct in careTakers) CareTakers.Add(ct);
                });
            }
        }

        // Gdy opiekun wybierze podopiecznego z listy (Pickera)
        partial void OnSelectedCareTakerChanged(User? value)
        {
            if (value != null)
            {
                IsQuestionsVisible = true;
                IsEditorVisible = false;
                _ = LoadQuestionsAsync(value.Id);
            }
            else
            {
                IsQuestionsVisible = false;
            }
        }

        private async Task LoadQuestionsAsync(string careTakerId)
        {
            var questions = await _firestoreService.GetQuestionTemplatesAsync(careTakerId);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Questions.Clear();
                foreach (var q in questions) Questions.Add(q);
            });
        }

        // --- KOMENDY ZARZĄDZANIA PYTANIAMI ---

        [RelayCommand]
        private void OpenNewQuestionEditor()
        {
            _editingTemplate = new QuestionTemplate { OrderIndex = Questions.Count }; // Nowe pytanie na koniec
            EditorTitle = "Dodaj nowe pytanie";
            EditorQuestionText = string.Empty;
            EditorQuestionType = "Zamknięte";
            EditorOptions.Clear();
            EditorOptions.Add(new QuestionOption { Text = "Tak", Points = 5 });
            EditorOptions.Add(new QuestionOption { Text = "Nie", Points = 0 });

            IsEditorVisible = true;
        }

        [RelayCommand]
        private void OpenEditQuestionEditor(QuestionTemplate template)
        {
            if (template == null) return;

            _editingTemplate = template;
            EditorTitle = "Edytuj pytanie";
            EditorQuestionText = template.Text;
            EditorQuestionType = template.Type == "Open" ? "Otwarte" : "Zamknięte";

            EditorOptions.Clear();
            if (template.Options != null)
            {
                foreach (var opt in template.Options) EditorOptions.Add(opt);
            }

            IsEditorVisible = true;
        }

        [RelayCommand]
        private async Task DeleteQuestionAsync(QuestionTemplate template)
        {
            if (template == null || SelectedCareTaker == null) return;

            bool confirm = await Shell.Current.DisplayAlert("Usuń", "Czy na pewno chcesz usunąć to pytanie?", "Tak", "Nie");
            if (confirm)
            {
                await _firestoreService.DeleteQuestionTemplateAsync(SelectedCareTaker.Id, template.Id);
                await LoadQuestionsAsync(SelectedCareTaker.Id);
            }
        }

        // --- KOMENDY WEWNĄTRZ EDYTORA ---

        [RelayCommand]
        private void AddOption()
        {
            EditorOptions.Add(new QuestionOption { Text = "", Points = 0 });
        }

        [RelayCommand]
        private void RemoveOption(QuestionOption option)
        {
            if (option != null && EditorOptions.Contains(option))
            {
                EditorOptions.Remove(option);
            }
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditorVisible = false;
        }

        [RelayCommand]
        private async Task SaveQuestionAsync()
        {
            if (string.IsNullOrWhiteSpace(EditorQuestionText))
            {
                await Shell.Current.DisplayAlert("Błąd", "Treść pytania nie może być pusta.", "OK");
                return;
            }

            if (SelectedCareTaker == null || _editingTemplate == null) return;

            // Zaktualizuj edytowany model
            _editingTemplate.Text = EditorQuestionText;
            _editingTemplate.Type = EditorQuestionType == "Otwarte" ? "Open" : "Closed";

            _editingTemplate.Options = new List<QuestionOption>();
            if (_editingTemplate.Type == "Closed")
            {
                foreach (var opt in EditorOptions)
                {
                    if (!string.IsNullOrWhiteSpace(opt.Text))
                    {
                        _editingTemplate.Options.Add(new QuestionOption
                        {
                            Text = opt.Text,
                            Points = opt.Points
                        });
                    }
                }

                if (!_editingTemplate.Options.Any())
                {
                    await Shell.Current.DisplayAlert("Błąd", "Pytanie zamknięte musi mieć co najmniej jedną odpowiedź.", "OK");
                    return;
                }
            }

            // Zapis do Firebase
            await _firestoreService.SaveQuestionTemplateAsync(SelectedCareTaker.Id, _editingTemplate);

            IsEditorVisible = false;
            await LoadQuestionsAsync(SelectedCareTaker.Id);
        }
    }
}