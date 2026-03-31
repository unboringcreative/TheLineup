using UnityEngine;

[CreateAssetMenu(fileName = "SuspectProfile", menuName = "The Lineup/Suspect Profile")]
public class SuspectProfileSO : ScriptableObject
{
    public string displayName = "New Suspect";
    [TextArea(1, 2)] public string sex = "Unknown";
    [TextArea(1, 2)] public string occupation = "Unknown occupation";
    [TextArea(1, 2)] public string nationality = "Unknown nationality";
    [TextArea(1, 2)] public string height = "Unknown height";
    [TextArea(1, 2)] public string weight = "Unknown weight";
    [TextArea(1, 2)] public string keyPersonalityTrait = "Unknown personality trait";
    [TextArea(3, 8)] public string dialogue = "Interview dialogue placeholder.";
    public Sprite portrait;
}
