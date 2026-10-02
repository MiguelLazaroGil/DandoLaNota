using System;
using System.Collections;
using System.Collections.Generic;
using GVLoadSystem.Core;
using GVLoadSystem.GVEditor;
using Unity.Mathematics;
using UnityEngine;

namespace GVLoadSystem.Examples
{

    [CustomGVData("CustomEverything")]
    [Serializable]
    public class CustomEverything
    {
        [SaveAs("Hola")]
        public int adios = 0;

        [DontSave]
        public int dontSave;
        [GVMin(3)]
        public int min3 = 2;
        [GVMax(10)]
        public int max10 = 11;
        [GVRange(1, 10)]
        public float range1_10 = 12;
        [GVReadOnly]
        public string readonlyStr = "This is readonly";
        [WriteOnce]
        public char WriteOnceChar = 'a';

        [WriteN(3)]
        public char WriteThriceChar = 'c';
        public int __WriteThriceCharWriteCount;


        public enum CustomEnumforEvthng { ONE, TWO, THREE }
        public CustomEnumforEvthng customEnumforEvthng;

        public CustomExample example;

        public
        int a;
        public
        Vector3 vector;
        [SerializeField]
        public BoundsInt boundsInt;
        public LayerMask layerMask;
        public Bounds bounds;

        public Gradient gradient;
        public Rect rect;
        public RectInt rectInt;
        public Vector4 vector4;
        public Vector2Int vector2Int;
        public List<int> listaEnteros;
        public List<char> listaChar;
        public Color colorUnity;
        public CustomPlayerData playerData;
        public
        CustomColorData color;
        public Sprite sprite;
        public GameObject go;
        public Quaternion quaternion;
        public Transform transform;
        public AnimationCurve animationCurve;
    }
    [CustomGVData("CustomExample")]
    [Serializable]
    public class CustomExample
    {
        public
        int hola;
        [SerializeField] CustomColorData color;
    }
}