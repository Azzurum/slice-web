using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace SLICE_Website.Models
{
    public class MenuItem : INotifyPropertyChanged
    {
        private string _productName;
        private decimal _basePrice;
        private bool _isAvailable;
        private string _imagePath;

        public int ProductID { get; set; }

        public ObservableCollection<RecipeItemVM> Recipe { get; set; } = new ObservableCollection<RecipeItemVM>();

        public string ProductName
        {
            get => _productName;
            set { _productName = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(VirtualCategory)); }
        }

        public decimal BasePrice
        {
            get => _basePrice;
            set { _basePrice = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedPrice)); }
        }

        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                _isAvailable = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(Opacity));
            }
        }

        // This stores ONLY the filename (e.g., "pizza.png") in the database
        public string ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullImagePath)); // Tells UI to reload the image
            }
        }

        // --- UI HELPERS ---

        // Dynamically rebuilds the path based on the computer running the app
        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ImagePath)) return null;
                return System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "Menu", ImagePath);
            }
        }

        // Extract "Pizza" from "Pizza | Pepperoni"
        public string VirtualCategory
        {
            get => ProductName.Contains("|") ? ProductName.Split('|')[0].Trim() : "General";
        }

        // Extract "Pepperoni" from "Pizza | Pepperoni"
        public string DisplayName
        {
            get => ProductName.Contains("|") ? ProductName.Split('|')[1].Trim() : ProductName;
        }

        public string FormattedPrice => $"₱{BasePrice:N2}";

        public string StatusText => IsAvailable ? "Available" : "Unavailable";

        // Green for Available, Red/Gray for Unavailable
        public string StatusColor => IsAvailable ? "#27AE60" : "#C0392B";

        public double Opacity => IsAvailable ? 1.0 : 0.6;

        // MVVM Event Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}