// I_Damageable.cs
public interface I_StatModifiable
{
    public void AddModifier(Sc_Modifier modifier);
    public void RemoveModifier(Sc_Modifier modifier);
    public void ClearAllModifiers( );
  
}