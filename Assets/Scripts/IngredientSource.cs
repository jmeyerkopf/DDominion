using UnityEngine;

public class IngredientSource : MonoBehaviour
{
    public IngredientType type;
    public int quantity = 1;
    public string ingredientSourceName = "Ingredient Source"; // For logging

    void Start()
    {
        if (string.IsNullOrEmpty(ingredientSourceName) || ingredientSourceName == "Ingredient Source")
        {
            ingredientSourceName = type.ToString() + " Source"; // Default name based on type
        }
    }
}
