using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameVSM
{
    public sealed class TrainVisibility : MonoBehaviour
    {
        [Serializable]
        public struct Zone
        {
            public GameObject Root;
            public Bounds Bounds;
            public bool Interior;
        }
        public Zone[] Zones;
        public Transform Observer;
        Renderer[][] surfaces;
        ShadowCastingMode[][] originalShadows;
        bool[] detailedShadows;
        float nextCheck;
        void Start()
        {
            if(Zones==null)Zones=Array.Empty<Zone>();
            surfaces=new Renderer[Zones.Length][];
            originalShadows=new ShadowCastingMode[Zones.Length][];
            detailedShadows=new bool[Zones.Length];
            for(int i=0;i<Zones.Length;i++)
            {
                surfaces[i]=Zones[i].Root==null?Array.Empty<Renderer>():Zones[i].Root.GetComponentsInChildren<Renderer>(true);
                originalShadows[i]=new ShadowCastingMode[surfaces[i].Length];
                detailedShadows[i]=true;
                for(int j=0;j<surfaces[i].Length;j++)originalShadows[i][j]=surfaces[i][j].shadowCastingMode;
            }
        }
        void Update()
        {
            if (Observer == null || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .2f;
            float shadowRange=Application.isMobilePlatform?18:32;
            for(int i=0;i<Zones.Length;i++)
            {
                var zone=Zones[i];
                if(zone.Root==null)continue;
                // Whole-car roots include floors, doors, food and distant corridor views.
                // Keep all geometry; only reduce distant interior shadow casting.
                // Exterior shadows still enclose and shade the train normally.
                if (!zone.Root.activeSelf) zone.Root.SetActive(true);
                bool detailed=!zone.Interior || zone.Bounds.SqrDistance(Observer.position)<shadowRange*shadowRange;
                if(detailed==detailedShadows[i])continue;
                detailedShadows[i]=detailed;
                for(int j=0;j<surfaces[i].Length;j++)
                    if(surfaces[i][j]!=null)surfaces[i][j].shadowCastingMode=detailed?originalShadows[i][j]:ShadowCastingMode.Off;
            }
        }
    }
}
