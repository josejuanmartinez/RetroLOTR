using UnityEngine;

public class IllustrationsSmall : MonoBehaviour
{
    [Header("Leaders Icons")]
    public Sprite gandalf;
    public Sprite radagast;
    public Sprite beoraborn;
    public Sprite aragorn;
    public Sprite ecthelion;
    public Sprite imrahil;
    public Sprite elrond;
    public Sprite galadriel;
    public Sprite bain;
    public Sprite murazor;
    public Sprite sauron;
    public Sprite khamul;
    public Sprite ren;
    public Sprite hoarmurath;
    public Sprite dwar;
    public Sprite indur;
    public Sprite akhorahil;
    public Sprite adunaphel;
    public Sprite ovatha;
    public Sprite sangarunya;
    public Sprite haruth;
    public Sprite huz;
    public Sprite uvatha;
    public Sprite waulfa;
    public Sprite saruman;

    [Header("Other Characters")]
    public Sprite bilbo;
    public Sprite wormtongue;
    public Sprite themouth;

    [Header("Generic Characters")]
    public Sprite agent;
    public Sprite commander;
    public Sprite emmissary;
    public Sprite mage;

    public Sprite GetIllustrationByName(string name)
    {
        name = name.ToLower();
        // Get type information for this class
        System.Type type = this.GetType();

        // Get the field with the matching name (case-insensitive)
        System.Reflection.FieldInfo field = type.GetField(name,
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.IgnoreCase);

        // If we found a matching field, return its value as a Sprite
        if (field != null)
        {
            return field.GetValue(this) as Sprite;
        }

        // Return null if no matching field was found
        return null;
    }
}
