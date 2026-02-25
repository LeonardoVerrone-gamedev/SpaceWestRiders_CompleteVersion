using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Competition", menuName = "Racing/CompetitionSO")]
public class CompetitionSO : ScriptableObject
{
    public string competitionID;
    
    public List<CircuitSO> circuits;
    public int[] EliminateAt = {8, 16, 19};
    public bool enableCutscenes;
    public bool playerCanChooseCar;
    public TeamSO storyTeam;
    public TeamSO[] teams;

    public void Inicializar(List<CircuitSO> _circuits, int[] _eliminateAt = null, bool _enableCutscenes = false, bool _playerCanChooseCar = true, TeamSO _storyTeam = null)
    {
        if(_circuits != null) circuits = _circuits;
        if(_eliminateAt != null) EliminateAt = _eliminateAt;
        enableCutscenes = _enableCutscenes;
        playerCanChooseCar = _playerCanChooseCar;
        if(_storyTeam != null) storyTeam = _storyTeam;
    }
}
