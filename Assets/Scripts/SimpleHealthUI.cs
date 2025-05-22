using UnityEngine;
using UnityEngine.UI; // Required for UI Text and Image elements

public class SimpleHealthUI : MonoBehaviour
{
    public Text heroHealthText; 
    public Text scoutHealthText; 
    public Text goblinGoldText; 
    public Image detectionMeterFillImage; 
    public Text darkLordEnergyText; // For Dark Lord's Evil Energy

    private Health heroHealthComponent; 
    private Health scoutHealthComponent;
    private GameObject lastFoundScout; 

    public HeroController heroController; 
    public DarkLordAIController darkLordAIController; // Changed to DarkLordAIController

    void Start()
    {
        // Hero references
        GameObject heroObject = GameObject.FindGameObjectWithTag("Hero");
        if (heroObject != null)
        {
            heroHealthComponent = heroObject.GetComponent<Health>();
            if (heroController == null) 
            {
                heroController = heroObject.GetComponent<HeroController>();
            }
            if (heroController == null) Debug.LogError("SimpleHealthUI: HeroController not found on Hero GameObject!");
            if (heroHealthComponent == null) Debug.LogError("SimpleHealthUI: Health component not found on Hero GameObject!");
        }
        else
        {
            Debug.LogError("SimpleHealthUI: Could not find GameObject with tag 'Hero'.");
        }

        // Dark Lord references
        if (darkLordAIController == null) // Changed variable name
        {
            GameObject darkLordObject = GameObject.Find("DarkLord"); 
            if (darkLordObject != null)
            {
                darkLordAIController = darkLordObject.GetComponent<DarkLordAIController>(); // Changed component type
            }
            if (darkLordAIController == null) Debug.LogError("SimpleHealthUI: DarkLordAIController not found on a 'DarkLord' GameObject or not assigned via Inspector.");
        }


        FindScoutHealth();

        // Null checks for UI elements
        if (heroHealthText == null) Debug.LogError("SimpleHealthUI: HeroHealthText not assigned!");
        if (scoutHealthText == null) Debug.LogError("SimpleHealthUI: ScoutHealthText not assigned!");
        if (goblinGoldText == null) Debug.LogError("SimpleHealthUI: GoblinGoldText not assigned!");
        if (detectionMeterFillImage == null) Debug.LogError("SimpleHealthUI: DetectionMeterFillImage not assigned!");
        else
        {
            GameObject meterParent = detectionMeterFillImage.transform.parent != null ? detectionMeterFillImage.transform.parent.gameObject : detectionMeterFillImage.gameObject;
            if(meterParent != detectionMeterFillImage.gameObject) meterParent.SetActive(false); 
            else detectionMeterFillImage.gameObject.SetActive(false); 
        }
        if (darkLordEnergyText == null) Debug.LogError("SimpleHealthUI: DarkLordEnergyText not assigned!");

    }

    void Update()
    {
        // Update Hero Health
        if (heroHealthComponent != null && heroHealthText != null && heroHealthComponent.gameObject.activeInHierarchy)
        {
            heroHealthText.text = "Hero Health: " + heroHealthComponent.GetCurrentHealth().ToString("F0") + " / " + heroHealthComponent.GetMaxHealth().ToString("F0");
        }
        else if (heroHealthText != null)
        {
            heroHealthText.text = "Hero Health: Defeated";
        }

        // Update Scout Health
        if (scoutHealthComponent == null || (lastFoundScout != null && !lastFoundScout.activeInHierarchy) )
        {
            FindScoutHealth(); 
        }
        
        if (scoutHealthComponent != null && scoutHealthText != null && scoutHealthComponent.gameObject.activeInHierarchy)
        {
            scoutHealthText.text = "Scout Health: " + scoutHealthComponent.GetCurrentHealth().ToString("F0") + " / " + scoutHealthComponent.GetMaxHealth().ToString("F0");
        }
        else if (scoutHealthText != null)
        {
            scoutHealthText.text = "Scout Health: None/Defeated";
        }

        // Update Gold Display
        if (heroController != null && goblinGoldText != null)
        {
            goblinGoldText.text = "Gold: " + heroController.goldAmount;
        }
        else if (goblinGoldText != null)
        {
            goblinGoldText.text = "Gold: N/A";
        }

        // Update Detection Meter
        if (heroController != null && detectionMeterFillImage != null)
        {
            detectionMeterFillImage.fillAmount = heroController.detectionLevel;
            bool shouldBeVisible = heroController.detectionLevel > 0.01f && !heroController.IsHidden;
            
            GameObject meterObjectToToggle = detectionMeterFillImage.gameObject;
            if (detectionMeterFillImage.transform.parent != null && detectionMeterFillImage.transform.parent != this.transform && detectionMeterFillImage.transform.parent.GetComponent<RectTransform>() != null)
            {
                if (detectionMeterFillImage.transform.parent.gameObject.name == "DetectionMeterBackground")
                {
                    meterObjectToToggle = detectionMeterFillImage.transform.parent.gameObject;
                }
            }

            if (meterObjectToToggle.activeSelf != shouldBeVisible)
            {
                 meterObjectToToggle.SetActive(shouldBeVisible);
            }
        }
        else if (detectionMeterFillImage != null) 
        {
            GameObject meterObjectToToggle = detectionMeterFillImage.gameObject;
             if (detectionMeterFillImage.transform.parent != null && detectionMeterFillImage.transform.parent != this.transform && detectionMeterFillImage.transform.parent.GetComponent<RectTransform>() != null)
            {
                if (detectionMeterFillImage.transform.parent.gameObject.name == "DetectionMeterBackground")
                {
                    meterObjectToToggle = detectionMeterFillImage.transform.parent.gameObject;
                }
            }
            if(meterObjectToToggle.activeSelf) meterObjectToToggle.SetActive(false);
        }

        // Update Dark Lord Evil Energy
        if (darkLordAIController != null && darkLordEnergyText != null) // Changed variable name
        {
            darkLordEnergyText.text = "Evil Energy: " + Mathf.FloorToInt(darkLordAIController.currentEvilEnergy) + " / " + Mathf.FloorToInt(darkLordAIController.maxEvilEnergy);
        }
        else if (darkLordEnergyText != null)
        {
            darkLordEnergyText.text = "Evil Energy: N/A";
        }
    }

    void FindScoutHealth()
    {
        GameObject scoutObject = GameObject.FindGameObjectWithTag("Scout");
        if (scoutObject != null)
        {
            scoutHealthComponent = scoutObject.GetComponent<Health>();
            lastFoundScout = scoutObject; 
        }
        else
        {
            scoutHealthComponent = null; 
            lastFoundScout = null;
        }
    }
}
