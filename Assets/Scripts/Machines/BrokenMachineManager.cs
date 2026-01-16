using System.Collections.Generic;
using UnityEngine;

public class BrokenMachineManager : MonoBehaviour
{
    public static BrokenMachineManager Instance { get; private set; }

    [Header("Prefabs & UI")]
    [SerializeField] private BrokenMachineUI _ui;
    [SerializeField] private float _indicatorYOffset = 1.5f;

    // Track indicators per machine
    private readonly Dictionary<Machine, GameObject> _indicators = new Dictionary<Machine, GameObject>();

    // NEW: track broken particle effect per machine
    private readonly Dictionary<Machine, ParticleSystem> _brokenEffects = new Dictionary<Machine, ParticleSystem>();

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

        // Spawn particle effect (once per machine)
        if (machine.Data != null && machine.Data.brokenMachineParticleEffect != null && !_brokenEffects.ContainsKey(machine))
        {
            var fx = Instantiate(machine.Data.brokenMachineParticleEffect, position, Quaternion.identity);
            fx.transform.SetParent(machine.transform, worldPositionStays: true);
            fx.Play();
            _brokenEffects[machine] = fx;
        }

        // Spawn indicator (once per machine)
        if (_indicators.ContainsKey(machine)) return;

        GameObject indicatorPrefab = machine.Data != null ? machine.Data.brokenMachineIndicator : null;
        if (indicatorPrefab == null)
        {
            Debug.LogError($"BrokenMachineManager: No broken machine indicator prefab assigned for machine {(machine.Data != null ? machine.Data.machineName : machine.name)}.");
            return;
        }

        var indicator = Instantiate(indicatorPrefab);
        indicator.name = $"BrokenIndicator_{machine.name}";
        indicator.transform.SetParent(machine.transform, worldPositionStays: false);
        indicator.transform.localPosition = Vector3.up * _indicatorYOffset;

        var indicatorComp = indicator.GetComponent<BrokenMachineIndicator>();
        if (indicatorComp == null) indicatorComp = indicator.AddComponent<BrokenMachineIndicator>();
        indicatorComp.Attach(machine, this);

        _indicators[machine] = indicator;
    }

    private void HandleMachineRepaired(Machine machine)
    {
        if (machine == null) return;

        // Stop/destroy the effect for this specific machine
        if (_brokenEffects.TryGetValue(machine, out var fx) && fx != null)
        {
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(fx.gameObject);
        }
        _brokenEffects.Remove(machine);

        if (_indicators.TryGetValue(machine, out var go) && go != null)
            Destroy(go);
        _indicators.Remove(machine);

        if (_ui != null && _ui.IsOpenFor(machine))
            _ui.Close();
    }

    public void OpenRepairUI(Machine machine)
    {
        if (_ui == null || machine == null) return;
        _ui.OpenFor(machine);
    }

    public void Repair(Machine machine)
    {
        if (machine == null) return;
        machine.Repair();
    }
}