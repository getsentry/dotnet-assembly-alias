using Mono.Cecil;

static class CecilExtensions
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

    public static string PublicKeyString(this StrongNameKeyPair key) =>
        string.Concat(key.PublicKey.Select(_ => _.ToString("x2")));
}