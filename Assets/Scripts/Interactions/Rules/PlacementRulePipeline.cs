using System.Collections.Generic;
using UnityEngine;

public class PlacementRulePipeline 
{
    private readonly List<IPlacementRule> _rules = new List<IPlacementRule>();

    public PlacementRulePipeline Add(IPlacementRule rule)
    {
        if (rule != null) _rules.Add(rule);
        return this;
    }

    public bool Validate(GridService grid, IGridOccupant occ, Vector2Int anchor, GridOrientation orientation, out string error)
    {
        foreach (var r in _rules)
        {
            if (!r.Validate(grid, occ, anchor, orientation))
            {
                error = r.Error;
                return false;
            }
        }
        error = null;
        return true;
    }
}
