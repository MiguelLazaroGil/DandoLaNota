using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GVLoadSystem.Interfaces
{
    public interface ILoaderUser
    {

        public void SubscribeToValuesChange();


        public void OnValuesChange();

    }
}