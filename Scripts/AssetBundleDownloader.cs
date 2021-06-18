using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Plugins.AssetBundleDownloader.Scripts
{
    /// <summary>
    /// Handles downloading AssetBundles and their metadata
    /// </summary>
    public class AssetBundleDownloader : MonoBehaviour
    {
        /// <summary>
        /// List of URLs to request JSON bundle listings from
        /// </summary>
        public List<string> BundleSources = new List<string> { "https://unity-assetloader-test.s3.us-east-2.amazonaws.com/bundles_info.json" };

        /// <summary>
        /// Unloads all AssetBundles in OnEnable
        /// </summary>
        public bool UnloadBundlesOnEnable = true;

        /// <summary>
        /// Bundles already loaded by program
        /// </summary>
        [HideInInspector]
        public static Dictionary<string, AssetBundle> DownloadedBundles = new Dictionary<string, AssetBundle>();

        /// <summary>
        /// Bundles with metadata downloaded 
        /// </summary>
        [HideInInspector]
        public static Dictionary<string, BundleMetadata> KnownBundles = new Dictionary<string, BundleMetadata>();
        
        /// <summary>
        /// Trimmed platform name for session
        /// </summary>
        [HideInInspector]
        public static string Platform { get; } = Application.platform.ToString().Replace("Editor", "").Replace("Player", "");

        private void OnEnable()
        {
            if (UnloadBundlesOnEnable)
            {
                UnloadBundles();
            }

        }

        /// <summary>
        /// Unloads all downloaded bundles
        /// </summary>
        internal static void UnloadBundles()
        {
            foreach (var bundle in DownloadedBundles.Values)
            {
                bundle?.Unload(true);
            }

            DownloadedBundles.Clear();
        }

        /// <summary>
        /// Collection of bundles filtered to the current platform
        /// </summary>
        public static IEnumerable<BundleMetadata> CompatibleBundles
        {
            get { return KnownBundles.Values.Where(bundle => bundle.bundles.ContainsKey(Platform)); }
        }

        /// <summary>
        /// Request bundle metadata listings from all sources
        /// </summary>
        /// <returns></returns>
        public async Task DownloadAllMetadata() => await Task.WhenAll(BundleSources.Select(getBundlesList));

        /// <summary>
        /// Request list of bundle IDs from the API server
        /// </summary>
        /// <param name="source">URL to load list from</param>
        /// <returns></returns>
        internal async Task getBundlesList(string source)
        {
            var request = UnityWebRequest.Get(source);
            request.SendWebRequest();

            // Wait for request to complete
            while (!request.isDone)
            {
                await Task.Delay(15);
            }

            // Handle error case
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new WebException("Could not make API request: " + request.error);
            }

            var deserialized = JsonConvert.DeserializeObject<Dictionary<string, BundleMetadata>>(request.downloadHandler.text);
            
            foreach (string bundleName in deserialized.Keys)
            {
                if (KnownBundles.ContainsKey(bundleName))
                {
                    // Replace if incoming bundle metadata is newer
                    if (KnownBundles[bundleName].lastUpdated < deserialized[bundleName].lastUpdated)
                    {
                        KnownBundles.Add(bundleName, deserialized[bundleName]);
                    }
                }
                else
                {
                    // Add to dict
                    KnownBundles.Add(bundleName, deserialized[bundleName]);
                }
            }
        }
        
        /// <summary>
        /// Request the metadata for a given bundle
        /// </summary>
        /// <param name="bundleName"></param>
        /// <returns></returns>
        public static BundleMetadata GetBundleMetadata(string bundleName)
        {
            // Handle error case
            if (!KnownBundles.ContainsKey(bundleName))
            {
                throw new Exception("Could not retrieve bundles list");
            }

            return KnownBundles[bundleName];
        }

        /// <summary>
        /// Download the AssetBundles with a given filename
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static async Task<AssetBundle> GetBundle(string filename)
        {
            string bundleName = filename;
            Debug.Log($"AssetBundle {bundleName}");

            if (DownloadedBundles.ContainsKey(bundleName))
            {
                Debug.Log($"Found existing AssetBundle for {bundleName}");
                return DownloadedBundles[bundleName];
            }
            else
            {
                Debug.Log($"No existing AssetBundle for {bundleName}");
            }

            UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(filename);

            request.SendWebRequest();

            // Wait for request to complete
            while (!request.isDone)
            {
                await Task.Delay(15);
            }

            // Handle error case
            if (request.responseCode != 200)
            {
                throw new WebException("Could not make API request");
            }

            var assetBundle = DownloadHandlerAssetBundle.GetContent(request);
            DownloadedBundles.Add(bundleName, assetBundle);

            return assetBundle;
        }
    }
    
    /// <summary>
    /// Metadata of a bundle
    /// </summary>
    [Serializable]
    public class BundleMetadata
    {
        public string name;
        public string description;
        public Dictionary<string, List<string>> bundles;
        public List<string> tags;
        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string error;
        public string author;
        public long lastUpdated;
    }
}
