using UnityEngine;

[CreateAssetMenu(fileName = "EvidenceProfile", menuName = "The Lineup/Evidence Profile")]
public class EvidenceProfileSO : ScriptableObject
{
    public string title = "Evidence";
    [TextArea(2, 5)] public string description = "Evidence notes.";
    [TextArea(1, 3)] public string discoveryLocation = "Unknown location";
    public Sprite image;
}
