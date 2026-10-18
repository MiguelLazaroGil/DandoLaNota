using System;
using System.Collections.Generic;
using UnityEngine;

public class RandomHairStyle : MonoBehaviour
{
    [SerializeField]
    List<GameObject> HairStyles;
    [SerializeField]
    List<Material> HairColors;
    void OnEnable()
    {
        DeactivateHairStyles();
        int randomHairStyleIdx = UnityEngine.Random.Range(0, HairStyles.Count + 1);
        if (randomHairStyleIdx < HairStyles.Count)
        {
            HairStyles[randomHairStyleIdx].gameObject.SetActive(true);
            int randomMatStyleIdx = UnityEngine.Random.Range(0, HairColors.Count);
            HairStyles[randomHairStyleIdx].gameObject.GetComponent<MeshRenderer>().material = HairColors[randomMatStyleIdx];
        }

    }
    private void DeactivateHairStyles()
    {
        foreach (var hair in HairStyles)
            hair.gameObject.SetActive(false);
    }
}
