using System.Collections.Generic;
using UnityEngine;

public class BrokenMachineManager : MonoBehaviour
{
    public static BrokenMachineManager Instance { get; private set; }

    [Header("Prefabs & UI")]
    [SerializeField] private BrokenMachineUI _ui;
    [SerializeField] private float _indicatorYOffset = 1.5f;

    private ParticleSystem _brokenEffect;
    // Track indicators per machine
    private readonly Dictionary<Machine, GameObject> _indicators = new Dictionary<Machine, GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        Machine.OnMachineBroken += HandleBrokenMachine;
        Machine.OnMachineRepaired += HandleMachineRepaired;
    }

    private void OnDisable()
    {
        Machine.OnMachineBroken -= HandleBrokenMachine;
        Machine.OnMachineRepaired -= HandleMachineRepaired;
    }

    private void HandleBrokenMachine(Machine machine, Vector3 position)
    {
        if (machine == null) return;
        if (_indicators.ContainsKey(machine)) return;

        //Spawn particle effect
        if (machine.Data.brokenMachineParticleEffect != null)
        {
            _brokenEffect = Instantiate(machine.Data.brokenMachineParticleEffect, position, Quaternion.identity);
            _brokenEffect.transform.SetParent(machine.transform, worldPositionStays: true);
            _brokenEffect.Play();
        }

        // Choose indicator: minigame-specific prefab if provided, else fallback
        GameObject indicatorPrefab = machine.Data.brokenMachineIndicator;

        if (indicatorPrefab == null)
        {
            Debug.LogError($"BrokenMachineManager: No broken machine indicator prefab assigned for machine {machine.Data.machineName}.");
            return;
        }

        // Spawn and parent to machine so it follows it
        var indicator = Instantiate(indicatorPrefab);
        indicator.name = $"BrokenIndicator_{machine.name}";
        indicator.transform.SetParent(machine.transform, worldPositionStays: false);
        indicator.transform.localPosition = Vector3.up * _indicatorYOffset;

        // Ensure it has a script to handle tap -> open UI
        var indicatorComp = indicator.GetComponent<BrokenMachineIndicator>();
        if (indicatorComp == null) indicatorComp = indicator.AddComponent<BrokenMachineIndicator>();
        indicatorComp.Attach(machine, this);

        _indicators[machine] = indicator;
    }

    private void HandleMachineRepaired(Machine machine)
    {
        if (machine == null) return;

        _brokenEffect?.Stop();
        _brokenEffect = null;

        if (_indicators.TryGetValue(machine, out var go) && go != null)
        {
            Destroy(go);
        }
        _indicators.Remove(machine);
        if (_ui != null && _ui.IsOpenFor(machine))
        {
            _ui.Close();
        }
    }

    // Called by indicator or machine tap
    public void OpenRepairUI(Machine machine)
    {
        if (_ui == null || machine == null) return;
        _ui.OpenFor(machine);
    }

    // Called by UI
    public void Repair(Machine machine)
    {
        if (machine == null) return;
        machine.Repair();
    }
}