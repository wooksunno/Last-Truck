using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace StylizedWater3
{
    public static class Integrations
    {
        [Serializable]
        public class Integration<TAsset> where TAsset : Enum
        {
            public string name;
            public int id;
            public readonly TAsset asset;
            public string url;
            
            public string libraryGUID;
            public bool includeWithPragmas;
            private Texture2D _thumbnail;
            public Texture2D thumbnail => _thumbnail ??= UI.Icons.DownloadAssetIcon(id);
            
            public bool underwaterCompatible;
            public bool installed;

            public Integration(string name, TAsset asset, int id, bool includeWithPragmas, bool uwc, string guid, string url)
            {
                this.name = name;
                this.asset = asset;
                this.id = id;
                this.underwaterCompatible = uwc;
                this.includeWithPragmas = includeWithPragmas;
                this.libraryGUID = guid;
                this.url = url;
            }
        }

        private static bool IsLibraryPresent<TAsset>(Integration<TAsset> integration, params TAsset[] alwaysInstalledAssets) where TAsset : Enum
        {
            for (int i = 0; i < alwaysInstalledAssets.Length; i++)
            {
                if (integration.asset.Equals(alwaysInstalledAssets[i]))
                {
                    return true;
                }
            }

            string path = AssetDatabase.GUIDToAssetPath(integration.libraryGUID);

            return path != string.Empty && AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset));
        }

        private static Integration<TAsset> GetIntegration<TAsset>(Integration<TAsset>[] integrations, TAsset asset) where TAsset : Enum
        {
            for (int i = 0; i < integrations.Length; i++)
            {
                if (integrations[i].asset.Equals(asset))
                {
                    return integrations[i];
                }
            }

            return null;
        }

        private static Integration<TAsset> GetFirstInstalledAsset<TAsset>(Integration<TAsset>[] integrations, TAsset fallbackAsset, params TAsset[] alwaysInstalledAssets) where TAsset : Enum
        {
            for (int i = 0; i < integrations.Length; i++)
            {
                bool alwaysInstalled = false;

                #if SWS_DEV
                //Do not use in dev project, gets in the way of using/testing the default fog
                if(integrations[i].asset.GetType() == typeof(Integrations.FogAssets) && integrations[i].asset.CompareTo(FogAssets.SCScreenEffects) == 0) continue;
                #endif
                
                for (int j = 0; j < alwaysInstalledAssets.Length; j++)
                {
                    if (integrations[i].asset.Equals(alwaysInstalledAssets[j]))
                    {
                        alwaysInstalled = true;
                        break;
                    }
                }

                if (alwaysInstalled)
                {
                    continue;
                }

                if (IsLibraryPresent(integrations[i], alwaysInstalledAssets))
                {
                    return integrations[i];
                }
            }

            return GetIntegration(integrations, fallbackAsset);
        }

        private static void UpdateInstalledStates<TAsset>(Integration<TAsset>[] integrations, params TAsset[] alwaysInstalledAssets) where TAsset : Enum
        {
            for (int i = 0; i < integrations.Length; i++)
            {
                integrations[i].installed = IsLibraryPresent(integrations[i], alwaysInstalledAssets);
            }
        }

        #region Fog
        public enum FogAssets
        {
            None,
            [InspectorName("Default Unity")]
            UnityFog,
            [InspectorName("COZY: Stylized Weather v3")]
            COZY3,
            AtmosphericHeightFog,
            [InspectorName("Enviro 3 - Sky & Weather")]
            Enviro3,
            [InspectorName("Azure[Sky] Dynamic Skybox")]
            Azure,
            [InspectorName("Buto (2022+)")]
            Buto,
            [InspectorName("SC Screen Effects")]
            SCScreenEffects,
            [InspectorName("COZY: Stylized Weather v4")]
            COZY4
        }

        [NoAutoStaticsCleanup]
        private static Integration<FogAssets>[] _FogIntegrations;

        public static Integration<FogAssets>[] FogIntegrations
        {
            get
            {
                if (_FogIntegrations == null)
                {
                    _FogIntegrations = GetAvailableFogIntegrations();
                }

                return _FogIntegrations;
            }
        }

        private static Integration<FogAssets>[] GetAvailableFogIntegrations()
        {
            Integration<FogAssets>[] integrationsArray = new[]
            {
                new Integration<FogAssets>("None", FogAssets.None, 0, true, true, "", ""),
                new Integration<FogAssets>("Default Unity", FogAssets.UnityFog, 0, true, true, "", ""),
                new Integration<FogAssets>("SC Screen Effects (Screen-Space Fog)", FogAssets.SCScreenEffects, 377136, true, true, "b0ad0918009d7454c846bbd229f6aec4", "https://assetstore.unity.com/packages/slug/377136"),
                new Integration<FogAssets>("COZY: Stylized Weather v3", FogAssets.COZY3, 271742, true, true, "e9bc566e199a97947a3e600f2563fe85", "https://assetstore.unity.com/packages/slug/271742"),
                new Integration<FogAssets>("COZY: Stylized Weather v4", FogAssets.COZY4, 389284, false, true, "23eb33f4988c6c04ab529e639c603dd5", "https://assetstore.unity.com/packages/slug/389284"),
                new Integration<FogAssets>("Enviro 3 - Sky & Weather", FogAssets.Enviro3, 236601, true, true, "8db9bd7b531f93d46ae2cb21180a00a8", "https://assetstore.unity.com/packages/slug/236601"),
                new Integration<FogAssets>("Atmospheric Height Fog", FogAssets.AtmosphericHeightFog, 143825, false, true, "8db8edf9bba0e9d48998019ca6c2f9ff", "https://assetstore.unity.com/packages/slug/143825"),
                new Integration<FogAssets>("Azure[Sky] Dynamic Skybox", FogAssets.Azure, 36050, true, true, "5da6209b193d6eb49b25c88c861e52fa", "https://assetstore.unity.com/packages/slug/36050"),
                new Integration<FogAssets>("Buto - Volumetric Fog and Volumetric Lighting", FogAssets.Buto, 258881, true, true, "ada48e5159bb1a5469288f9c75ca4629", "https://assetstore.unity.com/packages/slug/258881"),
            };

            UpdateInstalledStates(integrationsArray, FogAssets.None, FogAssets.UnityFog);

            return integrationsArray;
        }

        public static Integration<FogAssets> GetFogIntegration(FogAssets asset)
        {
            return GetIntegration(FogIntegrations, asset);
        }

        public static bool IsFogLibraryPresent(Integration<FogAssets> integration)
        {
            return IsLibraryPresent(integration, FogAssets.None, FogAssets.UnityFog);
        }

        public static Integration<FogAssets> GetFirstInstalledFogAsset()
        {
            return GetFirstInstalledAsset(FogIntegrations, FogAssets.UnityFog, FogAssets.None, FogAssets.UnityFog);
        }

        #endregion

        #region Rendering
        public enum RenderingAssets
        {
            [InspectorName("None")]
            None,
            [InspectorName("GigaLights")]
            GigaLights
        }

        [NoAutoStaticsCleanup]
        private static Integration<RenderingAssets>[] _RenderingIntegrations;

        public static Integration<RenderingAssets>[] RenderingIntegrations
        {
            get
            {
                if (_RenderingIntegrations == null)
                {
                    _RenderingIntegrations = GetAvailableRenderingIntegrations();
                }

                return _RenderingIntegrations;
            }
        }

        private static Integration<RenderingAssets>[] GetAvailableRenderingIntegrations()
        {
            Integration<RenderingAssets>[] integrationsArray = new[]
            {
                new Integration<RenderingAssets>("None", RenderingAssets.None, 0, true, true, "", ""),
                new Integration<RenderingAssets>("GigaLights", RenderingAssets.GigaLights, 397894, true, true, "50d3c93cc3614a90a9379d1d52ed6bdb", "https://assetstore.unity.com/packages/slug/397894"),
            };

            UpdateInstalledStates(integrationsArray, RenderingAssets.None);

            return integrationsArray;
        }

        public static Integration<RenderingAssets> GetRenderingIntegration(RenderingAssets asset)
        {
            return GetIntegration(RenderingIntegrations, asset);
        }

        public static bool IsRenderingLibraryPresent(Integration<RenderingAssets> integration)
        {
            return IsLibraryPresent(integration, RenderingAssets.None);
        }

        public static Integration<RenderingAssets> GetFirstInstalledRenderingAsset()
        {
            return GetFirstInstalledAsset(RenderingIntegrations, RenderingAssets.None, RenderingAssets.None);
        }
        #endregion
    }
}