using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CaseLibrary", menuName = "The Lineup/Case Library")]
public class CaseLibrarySO : ScriptableObject
{
    public List<CaseDefinitionSO> cases = new List<CaseDefinitionSO>();
}
