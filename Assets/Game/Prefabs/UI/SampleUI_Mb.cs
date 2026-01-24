//using UnityEngine;

//public class SampleUI_Mb : MonoBehaviour
//{
//    private bool _showUI = true;

//    void OnGUI( )
//    {
//        if (!_showUI) return;

//        // Button size
//        float buttonWidth = 200f;
//        float buttonHeight = 60f;

//        // Center position
//        float x = ( Screen.width - buttonWidth ) * 0.5f;
//        float y = ( Screen.height - buttonHeight ) * 0.5f;

//        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Rajah Bagwis"))
//        {
//            _showUI = false;
//        }
//    }
//    public static void SetLoadout(Guardian_SO guardianTemplate, AbilitySO abilityQ, AbilitySO abilityE)
//    {
//        SelectedStats = guardianTemplate;
//        SelectedAbilityQ = abilityQ;
//        SelectedAbilityE = abilityE;

//        Debug.Log($"Loadout Set: {guardianTemplate.characterName}");
//    }
//}
