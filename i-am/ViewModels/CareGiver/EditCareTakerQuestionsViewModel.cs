using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class EditorOptionItem : ObservableObject
    {
        [ObservableProperty] private string text = string.Empty;
        [ObservableProperty] private string points = "0";
    }

    public partial class QuestionItemViewModel : ObservableObject
    {
        public QuestionTemplate Template { get; set; } = new();

        public string Text => Template.Text;
        public string TypeText => Template.Type == "Open" ? "Format: Otwarte |" : "Format: Zamknięte |";

        public bool IsRandom => Template.IsRandomPool;
        public bool IsDaily => !Template.IsRandomPool;
        public bool IsClosed => Template.Type == "Closed";

        public string MaxSelectionsText => $"Max. odpowiedzi: {Template.MaxSelections}";
    }

    public partial class EditCareTakerQuestionsViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private QuestionTemplate? _editingTemplate;

        // ZMIANA 1: Zwykłe właściwości zmieniamy na obserwowalne przez [ObservableProperty]
        [ObservableProperty] private ObservableCollection<User> careTakers = new();
        [ObservableProperty] private ObservableCollection<QuestionItemViewModel> questions = new();
        [ObservableProperty] private ObservableCollection<EditorOptionItem> editorOptions = new();

        [ObservableProperty] private User? selectedCareTaker;
        [ObservableProperty] private string selectedCareTakerName = "Kliknij, aby wybrać...";

        [ObservableProperty] private bool isQuestionsVisible = false;
        [ObservableProperty] private bool isEditorVisible = false;
        [ObservableProperty] private bool isCareTakerSelectionVisible = true;

        [ObservableProperty] private bool isLoadingQuestions = false;
        [ObservableProperty] private bool hasNoQuestions = false;

        [ObservableProperty] private string editorTitle = string.Empty;
        [ObservableProperty] private string editorQuestionText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsClosedType))]
        [NotifyPropertyChangedFor(nameof(IsOpenType))]
        [NotifyPropertyChangedFor(nameof(ClosedTypeOpacity))]
        [NotifyPropertyChangedFor(nameof(OpenTypeOpacity))]
        private string editorQuestionType = "Zamknięte";

        public bool IsClosedType => EditorQuestionType == "Zamknięte";
        public bool IsOpenType => EditorQuestionType == "Otwarte";
        public double ClosedTypeOpacity => IsClosedType ? 1.0 : 0.3;
        public double OpenTypeOpacity => IsOpenType ? 1.0 : 0.3;

        [ObservableProperty] private bool editorIsRandomPool;
        [ObservableProperty] private int editorMaxSelections = 1;

        public EditCareTakerQuestionsViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task InitializeAsync()
        {
            if (CareTakers.Any()) return;

            string? myUid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(myUid)) return;

            var profile = await _firestoreService.GetUserProfileAsync(myUid);
            if (profile != null && profile.CaretakersID != null && profile.CaretakersID.Any())
            {
                var careTakersList = await _firestoreService.GetUsersByIdsAsync(profile.CaretakersID);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SelectedCareTaker = null;
                    SelectedCareTakerName = "Kliknij, aby wybrać...";
                    IsQuestionsVisible = false;
                    IsEditorVisible = false;
                    IsCareTakerSelectionVisible = true;
                    HasNoQuestions = false;

                    // ZMIANA 2: Przypisanie nowej kolekcji zamiast Clear() i Add() w pętli
                    CareTakers = new ObservableCollection<User>(careTakersList);
                });
            }
        }

        [RelayCommand]
        private async Task SelectCareTakerAsync()
        {
            if (!CareTakers.Any()) return;

            var names = CareTakers.Select(c => c.Name).ToArray();
            var action = await Shell.Current.DisplayActionSheet("Wybierz podopiecznego", "Anuluj", null, names);

            if (action != "Anuluj" && !string.IsNullOrEmpty(action))
            {
                var selected = CareTakers.FirstOrDefault(c => c.Name == action);
                if (selected != null)
                {
                    SelectedCareTaker = selected;
                    SelectedCareTakerName = selected.Name;
                    IsQuestionsVisible = true;
                    IsEditorVisible = false;
                    IsCareTakerSelectionVisible = true;
                    await LoadQuestionsAsync(selected.Id);
                }
            }
        }

        private async Task LoadQuestionsAsync(string careTakerId)
        {
            IsLoadingQuestions = true;
            HasNoQuestions = false;

            var questionsList = await _firestoreService.GetQuestionTemplatesAsync(careTakerId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // ZMIANA 3: Błyskawiczne ładowanie pytań - transformacja LINQ i jednorazowe przypisanie
                var newQuestions = questionsList
                    .OrderBy(x => x.OrderIndex)
                    .Select(q => new QuestionItemViewModel { Template = q });

                Questions = new ObservableCollection<QuestionItemViewModel>(newQuestions);

                IsLoadingQuestions = false;
                HasNoQuestions = Questions.Count == 0;
            });
        }

        [RelayCommand]
        private void SetQuestionType(string type)
        {
            EditorQuestionType = type;
            if (type == "Otwarte") EditorMaxSelections = 1;
        }

        [RelayCommand]
        private void DecreaseMaxSelections()
        {
            if (EditorMaxSelections > 1) EditorMaxSelections--;
        }

        [RelayCommand]
        private void IncreaseMaxSelections()
        {
            if (EditorMaxSelections < 10) EditorMaxSelections++;
        }

        [RelayCommand]
        private void OpenNewQuestionEditor()
        {
            _editingTemplate = new QuestionTemplate { OrderIndex = Questions.Count };
            EditorTitle = "Dodaj nowe pytanie";
            EditorQuestionText = string.Empty;
            EditorQuestionType = "Zamknięte";
            EditorIsRandomPool = false;
            EditorMaxSelections = 1;

            // ZMIANA 4: Szybkie przypisanie opcji startowych dla edytora
            EditorOptions = new ObservableCollection<EditorOptionItem>
            {
                new EditorOptionItem { Text = "Tak", Points = "5" },
                new EditorOptionItem { Text = "Nie", Points = "0" }
            };

            IsQuestionsVisible = false;
            IsEditorVisible = true;
            IsCareTakerSelectionVisible = false;
        }

        [RelayCommand]
        private void OpenEditQuestionEditor(QuestionTemplate template)
        {
            if (template == null) return;

            _editingTemplate = template;
            EditorTitle = "Edytuj pytanie";
            EditorQuestionText = template.Text;
            EditorQuestionType = template.Type == "Open" ? "Otwarte" : "Zamknięte";
            EditorIsRandomPool = template.IsRandomPool;
            EditorMaxSelections = template.MaxSelections < 1 ? 1 : template.MaxSelections;

            // ZMIANA 5: Błyskawiczne ładowanie istniejących odpowiedzi z bazy
            if (template.Options != null)
            {
                var loadedOptions = template.Options.Select(opt =>
                    new EditorOptionItem { Text = opt.Text, Points = opt.Points.ToString() });
                EditorOptions = new ObservableCollection<EditorOptionItem>(loadedOptions);
            }
            else
            {
                EditorOptions = new ObservableCollection<EditorOptionItem>();
            }

            IsQuestionsVisible = false;
            IsEditorVisible = true;
            IsCareTakerSelectionVisible = false;
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

        // UWAGA: Pojedyncze dodawanie i usuwanie opcji w edytorze zostaje bez zmian,
        // ponieważ reagują na kliknięcia użytkownika pojedynczo.
        [RelayCommand]
        private void AddOption() => EditorOptions.Add(new EditorOptionItem { Text = "", Points = "0" });

        [RelayCommand]
        private void RemoveOption(EditorOptionItem option)
        {
            if (option != null && EditorOptions.Contains(option)) EditorOptions.Remove(option);
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditorVisible = false;
            IsQuestionsVisible = true;
            IsCareTakerSelectionVisible = true;
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

            _editingTemplate.Text = EditorQuestionText;
            _editingTemplate.Type = EditorQuestionType == "Otwarte" ? "Open" : "Closed";
            _editingTemplate.IsRandomPool = EditorIsRandomPool;
            _editingTemplate.MaxSelections = _editingTemplate.Type == "Open" ? 1 : EditorMaxSelections;

            _editingTemplate.Options = new List<QuestionOption>();
            if (_editingTemplate.Type == "Closed")
            {
                foreach (var opt in EditorOptions)
                {
                    if (!string.IsNullOrWhiteSpace(opt.Text))
                    {
                        int.TryParse(opt.Points, out int parsedPoints);
                        _editingTemplate.Options.Add(new QuestionOption { Text = opt.Text, Points = parsedPoints });
                    }
                }

                if (!_editingTemplate.Options.Any())
                {
                    await Shell.Current.DisplayAlert("Błąd", "Pytanie zamknięte musi mieć co najmniej jedną odpowiedź.", "OK");
                    return;
                }
            }

            await _firestoreService.SaveQuestionTemplateAsync(SelectedCareTaker.Id, _editingTemplate);

            IsEditorVisible = false;
            IsQuestionsVisible = true;
            IsCareTakerSelectionVisible = true;
            await LoadQuestionsAsync(SelectedCareTaker.Id);
        }
    }
}