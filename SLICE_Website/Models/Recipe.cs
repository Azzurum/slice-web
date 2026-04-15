using System;

namespace SLICE_Website.Models
{
    public class Recipe
    {
        public int BillID { get; set; }
        public int ProductID { get; set; }      // The Sellable Item
        public int IngredientID { get; set; }   // The Raw Material
        public decimal RequiredQty { get; set; } // Amount needed per unit
    }
}