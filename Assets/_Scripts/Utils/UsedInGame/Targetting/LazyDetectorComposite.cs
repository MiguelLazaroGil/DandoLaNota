using System.Collections.Generic;
using UnityEngine;

public class LazyDetectorComposite : LazyDetector
{
    [SerializeField] private LazyDetector[] detectors;
    public override GameObject[] GetAllTargets()
    {
        if (detectors != null && detectors.Length > 0)
        {
            List<GameObject> allTargets = new List<GameObject>();
            foreach (var detector in detectors)
            {
                if (detector != null)
                {
                    var targets = detector.GetAllTargets();
                    foreach (var target in targets)
                    {
                        if (!allTargets.Contains(target))
                        {
                            allTargets.Add(target);
                        }
                    }
                }
            }
            var thistargets=  base.GetAllTargets();
            foreach (var target in thistargets)
            {
                if (!allTargets.Contains(target))
                {
                    allTargets.Add(target);
                }
            }
            return allTargets.ToArray();
        }
        return base.GetAllTargets();
    }
}
