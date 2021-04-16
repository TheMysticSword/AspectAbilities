using HG;
using HG.Coroutines;
using UnityEngine;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RoR2;

namespace TheMysticSword.AspectAbilities.ContentManagement
{
    public class ContentLoadHelper
    {
        public ReadableProgress<float> progress;
        public ParallelProgressCoroutine coroutine;

        public ContentLoadHelper()
        {
            progress = new ReadableProgress<float>();
            coroutine = new ParallelProgressCoroutine(progress);
        }

        public void DispatchLoad<OutType>(System.Type loadType, System.Action<OutType[]> onComplete = null)
        {
            if (!typeof(BaseLoadableAsset).IsAssignableFrom(loadType))
            {
                AspectAbilitiesPlugin.logger.LogError($"Attempted to load {loadType.Name} that does not inherit from {typeof(BaseLoadableAsset).Name}");
                return;
            }
            AsyncLoadingEnumerator<OutType> enumerator = new AsyncLoadingEnumerator<OutType>(loadType);
            enumerator.onComplete = onComplete;
            coroutine.Add(enumerator, enumerator.progressReceiver);
        }

        public class AsyncLoadingEnumerator<OutType> : IEnumerator<object>, IEnumerator, System.IDisposable
        {
            object IEnumerator<object>.Current
            {
                get
                {
                    return current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return current;
                }
            }

            public void Dispose() { }

            public void Reset() { }

            public System.Type current;
            public List<System.Type> types;
            public int position = 0;
            public List<OutType> loadedAssets = new List<OutType>();
            public System.Action<OutType[]> onComplete;
            public ReadableProgress<float> progressReceiver = new ReadableProgress<float>();

            public AsyncLoadingEnumerator(System.Type type)
            {
                types = Assembly.GetExecutingAssembly().GetTypes().Where(x => !x.IsAbstract && type.IsAssignableFrom(x)).ToList();
            }

            public bool done
            {
                get
                {
                    return position >= types.Count;
                }
            }

            public bool sorted = false;

            bool IEnumerator.MoveNext()
            {
                if (!done)
                {
                    current = types[position];

                    BaseLoadableAsset loadableAsset = (BaseLoadableAsset)System.Activator.CreateInstance(current);
                    loadableAsset.Load();
                    if (loadableAsset.asset != null) loadedAssets.Add((OutType)loadableAsset.asset);

                    position++;

                    progressReceiver.Report(Util.Remap(position / types.Count, 0f, 1f, 0f, 0.95f));
                }
                if (done)
                {
                    if (!sorted)
                    {
                        loadedAssets.Sort((x, y) => {
                            Object xObject = x as Object;
                            Object yObject = y as Object;
                            return string.Compare(xObject != null ? xObject.name : x.GetType().Name, yObject != null ? yObject.name : y.GetType().Name, System.StringComparison.OrdinalIgnoreCase);
                        });
                        progressReceiver.Report(0.97f);
                        sorted = true;
                        return true;
                    }
                    if (onComplete != null)
                    {
                        onComplete(loadedAssets.ToArray());
                    }
                    progressReceiver.Report(1f);
                    return false;
                }
                return true;
            }
        }

        public static void AddModPrefixToAssets<T>(RoR2.ContentManagement.NamedAssetCollection<T> namedAssetCollection) where T : Object
        {
            foreach (T asset in namedAssetCollection)
            {
                asset.name = AspectAbilitiesPlugin.TokenPrefix + asset.name;
            }
        }
    }

    public abstract class BaseLoadableAsset
    {
        public object asset;
        public abstract void OnLoad();
        public virtual void Load()
        {
            asset = this;
            OnLoad();
        }
    }
}
