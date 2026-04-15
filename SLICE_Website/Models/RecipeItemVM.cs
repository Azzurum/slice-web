using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SLICE_Website.Models
{
    public class RecipeItemVM : INotifyPropertyChanged
    {
        private decimal _requiredQty;

        public int IngredientID { get; set; }
        public string ItemName { get; set; }
        public string BaseUnit { get; set; } // e.g., "grams", "ml"

        public decimal RequiredQty
        {
            get => _requiredQty;
            set
            {
                _requiredQty = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}