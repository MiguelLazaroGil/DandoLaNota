#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GVLoadSystem.Core;
using GVUtils.Attributes;
using UnityEngine;

namespace GVLoadSystem.Examples
{
    public class SaveSystemTester : MonoBehaviour
    {
        [SerializeField][CustomLabel("KeyEntry")] string valueName;
        [SerializeField] VALUE_TYPE valueType;
        [SerializeField] GVEntryReference settingEntryReference;

        [ExposedScriptableObject][SerializeField] GroupValues gv;

        public GroupValuesWrapper<TestinWrapperClass> testin;


        [ContextMenu("Set Test Value")]
        [Button("Set Test Value with gvEntryReference")]

        void SetTestValue()
        {
            switch (valueType)
            {
                case VALUE_TYPE.BOOL:
                    settingEntryReference.Set<bool>(false);

                    break;

                case VALUE_TYPE.INT:
                    settingEntryReference.Set<int>(5);

                    break;

                case VALUE_TYPE.FLOAT:
                    settingEntryReference.Set<float>(6);

                    break;

                case VALUE_TYPE.STRING:
                    settingEntryReference.Set<string>("HW");

                    break;
                case VALUE_TYPE.CUSTOM:
                    var pData = new CustomPlayerData();

                    settingEntryReference.Set<CustomPlayerData>(pData);
                    Debug.Log($"Value: {pData.damage}");
                    break;
            }
        }
        [ContextMenu("Get Test Value with gvEntry")]
        [Button("Get Test Value with gvEntryReference")]
        void GetTestValue()
        {
            switch (valueType)
            {
                case VALUE_TYPE.BOOL:
                    bool boolValue = settingEntryReference.Get<bool>();
                    Debug.Log($"Bool Value: {boolValue}");
                    break;

                case VALUE_TYPE.INT:
                    int intValue = settingEntryReference.Get<int>();
                    Debug.Log($"Int Value: {intValue}");
                    break;

                case VALUE_TYPE.FLOAT:
                    float floatValue = settingEntryReference.Get<float>();
                    Debug.Log($"Float Value: {floatValue}");
                    break;

                case VALUE_TYPE.STRING:
                    string stringValue = settingEntryReference.Get<string>();
                    Debug.Log($"String Value: {stringValue}");
                    break;
                case VALUE_TYPE.CUSTOM:
                    var pData = new CustomPlayerData();
                    Debug.Log($"Value: {pData.damage}");

                    pData = settingEntryReference.Get<CustomPlayerData>();
                    Debug.Log($"Value: {pData.damage}");
                    break;
            }

        }
        // [Button("Test asyncrounous save")]
        // public async Task TestAsyncSave()
        // {
        //     await loader.SaveValuesAsync();
        //     Debug.Log("[SaveSystemTester]Valores guardados");
        // }
        // [Button("Test asyncrounous load")]
        // public async Task TestAsyncLoad()
        // {
        //     var gv = await loader.LoadValuesAsync();
        //     Debug.Log("[SaveSystemTester]Valores cargados" + gv.name);
        // }

        // [Button("AutoFindPath")]
        // public void autofindPath()
        // {
        //     loader.AutoResolveFromResources();
        // }
        // [Button("Create dynamic key for gv")]
        // public void creategvkey()
        // {
        //     GVEntry entry = new GVEntry()
        //     {

        //         name = valueName,
        //         type = VALUE_TYPE.BOOL,
        //         customTypeName = "",
        //         value = new BoolGVValue()
        //     };
        //     entry.value.SetValue(false);

        //     entry = new GVEntry()
        //     {

        //         name = valueName,
        //         type = VALUE_TYPE.CUSTOM,
        //         customTypeName = "PlayerData",
        //         value = new StringGVValue()
        //     };


        //     gv.TryAddEntry("TestingValues",entry);
        // }
        // [Button("Remove entry dinamically")]
        // public void removegvkey()
        // {
        //     gv.TryRemoveEntry(valueName);j
        // }
        // [Button("Get key async")]
        // public async Task getkeyasyn()
        // {



        //     ALoader loader=new();
        //     loader.SetEncrytionSettings(
        //         GroupValuesProjectSettings.instance.encryptionMethod,
        //         GroupValuesProjectSettings.instance.passwordSalt);
        //     loader.ChangeAssetName("Testing");
        //     loader.AutoResolveFromResources();
        //     loader.SetGroupValues(gv);

        //     var task=  loader.LoadValuesAsync();




        //     gv= task.Result;


        // }
        [Button("Create/Add SimpleGroupValues key")]
        public void createkey()
        {

            switch (valueType)
            {
                case VALUE_TYPE.BOOL:
                    SimpleGroupValues.Set<bool>(valueName, false);
                    break;

                case VALUE_TYPE.INT:
                    SimpleGroupValues.Set<int>(valueName, 6);
                    break;

                case VALUE_TYPE.FLOAT:
                    SimpleGroupValues.Set<float>(valueName, 5.0f);
                    break;

                case VALUE_TYPE.STRING:
                    SimpleGroupValues.Set<string>(valueName, "NewEntryValue");

                    break;

            }
            //  Debug.Log(SimpleGroupValues.Get<string>(valueName));
        }
        [Button("Get SimpleGroupValues value")]
        public void getkey()
        {

            switch (valueType)
            {
                case VALUE_TYPE.BOOL:
                    Debug.Log(SimpleGroupValues.Get<bool>(valueName));
                    break;

                case VALUE_TYPE.INT:
                    Debug.Log(SimpleGroupValues.Get<int>(valueName));
                    break;

                case VALUE_TYPE.FLOAT:
                    Debug.Log(SimpleGroupValues.Get<float>(valueName));
                    break;

                case VALUE_TYPE.STRING:
                    Debug.Log(SimpleGroupValues.Get<string>(valueName));

                    break;

            }
            //  Debug.Log(SimpleGroupValues.Get<string>(valueName));
        }

        [Button("Test Utility")]
        public void TestUtility()
        {
            GroupValues gv = GetComponent<LoaderMono>().LoadValues();
            Debug.Log("═══ GroupValuesUtility Test ═══");

            // GetAll & Count
            var floats = GroupValuesUtility.GetAll<float>(gv);
            var ints = GroupValuesUtility.GetAll<int>(gv);
            var strings = GroupValuesUtility.GetAll<string>(gv);
            var v3s = GroupValuesUtility.GetAll<Vector3>(gv);
            Debug.Log($"GetAll  → floats:{floats.Count}  ints:{ints.Count}  strings:{strings.Count}  v3:{v3s.Count}");

            // Scalar operations
            if (floats.Count > 0)
            {
                Debug.Log($"Float   → Sum:{GroupValuesUtility.SumAll<float>(gv):F2}  " +
                          $"Avg:{GroupValuesUtility.Average<float>(gv):F2}  " +
                          $"Min:{GroupValuesUtility.Min<float>(gv):F2}  " +
                          $"Max:{GroupValuesUtility.Max<float>(gv):F2}");
                Debug.Log($"Float   → Variance:{GroupValuesUtility.Variance<float>(gv):F4}  " +
                          $"StdDev:{GroupValuesUtility.StdDev<float>(gv):F4}  " +
                          $"Median:{GroupValuesUtility.Median<float>(gv):F2}");
            }
            // typedist — named pairs + formatted string
            var typedist = GroupValuesUtility.TypeDistribution(gv);
            if (typedist.Count > 0)
            {
                Debug.Log($"Type dist     → {GroupValuesUtility.FormatTypeDistribution(gv)}");
                // Individual access: pcts[0].key, pcts[0].pct
                foreach (var (key, pct) in typedist)
                    Debug.Log($"          {key}: {pct:F1}%");
            }
            // Percentages — named pairs + formatted string
            var pcts = GroupValuesUtility.CalculatePercentages(gv);
            if (pcts.Count > 0)
            {
                Debug.Log($"Pct     → {GroupValuesUtility.FormatPercentages(gv)}");
                // Individual access: pcts[0].key, pcts[0].pct
                foreach (var (key, pct) in pcts)
                    Debug.Log($"          {key}: {pct:F1}%");
            }

            // Normalize
            var norm = GroupValuesUtility.Normalize<float>(gv);
            if (norm.Count > 0)
                Debug.Log($"Norm    → [{string.Join(", ", norm.ConvertAll(n => $"{n:F2}"))}]");

            // Vector3
            if (v3s.Count > 0)
            {
                Debug.Log($"Vector3 → Sum:{GroupValuesUtility.SumAll<Vector3>(gv)}  " +
                          $"Avg:{GroupValuesUtility.Average<Vector3>(gv)}");
            }

            // Most used words — (word, count) pairs + formatted
            if (strings.Count > 0)
                Debug.Log($"Words   → {GroupValuesUtility.FormatMostUsedWords(gv, topN: 5)}");

            Debug.Log("═══ Test complete ═══");
        }
    }
    [Serializable]
    public class TestinWrapperClass
    {
        public int integervar;
        public float floatvar;
        public string stringvar;

    }
}
#endif