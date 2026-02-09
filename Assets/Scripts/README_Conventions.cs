// ------------------------------------
// ------------------------------------
// THIS IS NOT A SCRIPT FILE
// ------------------------------------
// This is a README file that outlines the coding conventions used in this project (Unity Built-in Render Pipeline).
// Please refer to this document when writing or reviewing code to ensure consistency across the codebase.

// ------------------------------------
// Naming Conventions
// ------------------------------------
// 1. Classes and Structs: Use PascalCase with descriptive prefix and underscore:
//      Script: Sc_PlayerController
//      Struct: Str_EnemyStats
//      MonoBehaviour: Mb_GuardianBase

// 2. Abstract and Interfaces: Use PascalCase prefixed with "I" and underscore, or "A" and underscore:
//      Interface: I_Damageable
//      Abstract Class: A_BaseAbility

// 3. Methods: Use PascalCase:
//      Method: TakeDamage

// 4. Public variables and fields, and SerializeFields: Use PascalCase:
//      Public Variable: MaxHealth
//      SerializeField: [SerializeField] private float AttackPower;

// 5. Private variables and fields: Use camelCase with an underscore prefix:
//      Private Variable: _currentHealth

// 6. Protected variables and fields: Use PascalCase with a single underscore prefix:
//      Protected Variable: _MoveSpeed

// 7. Constants: Use ALL_CAPS with underscores:
//      Constant: MAX_WAVES

// 8. Enums: Use PascalCase for enum names and ALL_CAPS for enum values:
//      Enum: GameState
//      Enum Values: STARTED, PAUSED, GAME_OVER

// 9. ScriptableObjects: Use PascalCase followed by "_SO":
//      ScriptableObject: Guardian_SO

// ------------------------------------
// Code Structure
// ------------------------------------

// 1. File Organization: Each script file should contain one class, struct, or interface. The filename should match the name of the class, struct, or interface it contains.

// 2. Indentation: Use tabs for indentation, and align code blocks properly for readability.

// 3. Braces: Opening braces should be on a new line for classes, methods, and control structures.
//      Example:
//      public class ExampleClass
//      {
//          public void ExampleMethod( )
//          {
//              if (condition)
//              {
//                  // code
//              }
//          }
//      }

// 4. Regions: Use #region and #endregion to organize large classes into logical sections (e.g., Fields, Properties, Methods).
//      Example:
//      #region Fields
//      private int _score;
//      #endregion

// 5. Comments: Use XML documentation comments (///) for public methods and classes. Use inline comments (//) sparingly to explain complex logic.
//      Example:
//      /// <summary>
//      /// Takes damage and updates health.
//      /// </summary>
//      /// <param name="amount">Amount of damage to take.</param>
//      public void TakeDamage(float amount)
//      {

// 6. Event Handling: Use C# events and delegates for communication between classes. Name events with the "On" prefix.
//      Example:
//      public event Action OnHealthChanged;
//      OnHealthChanged?.Invoke(newHealth);
// ------------------------------------

