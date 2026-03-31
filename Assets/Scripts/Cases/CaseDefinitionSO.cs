using UnityEngine;

[CreateAssetMenu(fileName = "CaseDefinition", menuName = "The Lineup/Case Definition")]
public class CaseDefinitionSO : ScriptableObject
{
    public string caseId = "case_001";
    public string caseTitle = "CASE 001";
    [TextArea(2, 4)] public string caseDescription = "A short overview of the case appears here.";
    [Header("Location")]
    public string locationAddressOrBusiness = string.Empty;
    public string locationCity = string.Empty;
    public string locationCountry = string.Empty;
    public Sprite featuredImage;
    [Tooltip("Exactly 5 slots are expected in the current scene layout.")]
    public SuspectProfileSO[] suspects = new SuspectProfileSO[5];

    [Tooltip("Exactly 3 slots are expected in the current scene layout.")]
    public EvidenceProfileSO[] evidence = new EvidenceProfileSO[3];

    [Range(0, 4)] public int guiltySuspectIndex;
    public string verdictTitle = "Verdict";
    [TextArea(3, 8)] public string explanation = "Explanation appears here.";
}
