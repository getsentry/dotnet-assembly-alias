using Mono.Cecil;

public static class CecilExtensions
{
    public static void MakeTypesInternal(this ModuleDefinition module)
    {
        foreach (var typeDefinition in module.Types)
        {
            typeDefinition.IsPublic = false;
        }
    }
    public static void SeyKey(this ModuleDefinition module, StrongNameKeyPair? key)
    {
        if (key == null)
        {
            module.Assembly.Name.PublicKey = null;
            module.Attributes &= ~ModuleAttributes.StrongNameSigned;
            return;
        }

        module.Assembly.Name.PublicKey = key.PublicKey;
    }
}