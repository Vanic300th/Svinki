public static class PreparePigClothing
{
    public static string Main()
    {
        UnityEditor.SessionState.SetString("Svinki.Clothing3D.Prepare","running");
        try { var result=PigClothingFit.RebuildAll(); UnityEditor.SessionState.SetString("Svinki.Clothing3D.Prepare",result); return result; }
        catch(System.Exception error){UnityEditor.SessionState.SetString("Svinki.Clothing3D.Prepare","FAILED: "+error.Message);throw;}
    }
}
