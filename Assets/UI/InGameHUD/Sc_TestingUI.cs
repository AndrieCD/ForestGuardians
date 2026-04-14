using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Sc_TestingUI : MonoBehaviour
{
    public TMP_Text Q, E, R, LMB, RMB, HP, Shield;
    public Image Healthbar, Shieldbar;

    public TMP_Text Stats;

    private void Awake()
    {
        // Ensure all references are assigned
        if (Q == null || E == null || R == null || LMB == null || RMB == null || Healthbar == null)
        {
            Debug.LogError("Sc_TestingUI: One or more UI references are not assigned in the inspector.");
        }

        // hook events
        Mb_HealthComponent playerHealth = FindFirstObjectByType<Mb_HealthComponent>();
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthbar;
            playerHealth.OnShieldChanged += UpdateShieldbar;
        }
        else
        {
            Debug.LogError("Sc_TestingUI: No Mb_HealthComponent found in the scene.");
        }

        
    }

    private void Start()
    {
        // hook ability cooldown events after all objects are initialized
        StartCoroutine(HookAbilityCooldowns());

    }

    private IEnumerator HookAbilityCooldowns()
    {
        yield return new WaitForSeconds(0.5f); // Wait one frame to ensure all objects are initialized
        Mb_AbilityController abilityController = FindFirstObjectByType<Mb_AbilityController>();
        if (abilityController != null)
        {
            abilityController.GetAbilityBySlot("Q").OnCooldownChanged += ToggleQ;
            abilityController.GetAbilityBySlot("E").OnCooldownChanged += ToggleE;
            //abilityController.GetAbilityBySlot("R").OnCooldownChanged += ToggleR;
            abilityController.GetAbilityBySlot("Primary").OnCooldownChanged += ToggleLMB;
            abilityController.GetAbilityBySlot("Secondary").OnCooldownChanged += ToggleRMB;
        }
    }

    private void Update()
    {
        Mb_StatBlock playerStats = GameObject.FindGameObjectWithTag("Player").GetComponent<Mb_StatBlock>();
        if (playerStats != null)
        {
            float ATK, AP, HST;
            ATK = playerStats.AttackPower.GetValue();
            AP = playerStats.AbilityPower.GetValue();
            HST = playerStats.Haste.GetValue();

            Stats.text = $"ATK: {ATK}\nAP: {AP}\nHASTE: {HST}";
            Debug.Log($"STATS: ATK: {ATK}, AP: {AP}, HASTE: {HST}");
        }

    }

    // Toggle Q
    private void ToggleQ(float cooldown)
    {
        if (cooldown > 0)
        {
            ToggleTextTransparency(true, Q);
        }
        else
        {
            ToggleTextTransparency(false, Q);
        }
    }

    // Toggle E
    private void ToggleE(float cooldown)
    {
        if (cooldown > 0)
        {
            ToggleTextTransparency(true, E);
        }
        else
        {
            ToggleTextTransparency(false, E);
        }
    }

    // Toggle R
    private void ToggleR(float cooldown)
    {
        if (cooldown > 0)
        {
            ToggleTextTransparency(true, R);
        }
        else
        {
            ToggleTextTransparency(false, R);
        }
    }

    // Toggle LMB
    private void ToggleLMB(float cooldown   )
    {
        if (cooldown > 0)
        {
            ToggleTextTransparency(true, LMB);
        }
        else
        {
            ToggleTextTransparency(false, LMB);
        }
    }

    // Toggle RMB
    private void ToggleRMB(float cooldown)
    {
        if (cooldown > 0)
        {
            ToggleTextTransparency(true, RMB);
        }
        else
        {
            ToggleTextTransparency(false, RMB);
        }
    }

    private void ToggleTextTransparency(bool isTransparent, TMP_Text txt)
    {
        if (isTransparent)
        {
            txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, 0.5f); // 50% transparent
        }
        else
        {
            txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, 1f); // Fully opaque
        }
    }

    // Healthbar update
    private void UpdateHealthbar(float currentHealth, float maxHealth)
    {
        if (Healthbar != null)
        {
            Healthbar.fillAmount = currentHealth / maxHealth;

            if (currentHealth / maxHealth <= 0.3f)
            {
                Healthbar.color = Color.red; // Danger
            }
            else if (currentHealth / maxHealth <= 0.6f)
            {
                Healthbar.color = Color.yellow; // Caution
            }
            else
            {
                Healthbar.color = Color.green; // Healthy
            }
        }

        if (HP != null)
        {
            HP.text = $"{currentHealth} / {maxHealth}";
        }
    }

    private void UpdateShieldbar(float currentShield)
    {
        if (Shieldbar != null)
        {
            Shieldbar.fillAmount = 1;
        }

        if (Shield != null)
        {
            Shield.text = $"{currentShield}";
        }
    }
}
