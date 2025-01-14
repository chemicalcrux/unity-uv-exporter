using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using System.Linq;
using ChemicalCrux.AttributeImporter.Transformers;
using UnityEditor;

namespace ChemicalCrux.AttributeImporter
{
    [ScriptedImporter(10, "attrdata")]
    public class DataImporter : ScriptedImporter
    {
        [System.Serializable]
        public class ObjectConfig
        {
            [HideInInspector] public string name;
        }

        [System.Serializable]
        public class AttributeConfig
        {
            [HideInInspector] public string name;
            [HideInInspector, SerializeField] internal bool exists;

            public List<AttributeTarget> targets;

            [SerializeReference, SerializeReferenceDropdown]
            public List<VertexTransformer> transformers;
        }

        /// <summary>
        /// Information about an attribute that currently exists in the attribute file.
        /// </summary>
        [System.Serializable]
        internal struct AttributeInfo
        {
            public string name;
            public int dimensions;
        }

        [SerializeField, HideInInspector] internal List<string> objectNames = new();
        [SerializeField, HideInInspector] internal List<AttributeInfo> activeAttributeInfo = new();

        [SerializeField] internal List<AttributeConfig> attributeConfigs = new();

        public override void OnImportAsset(AssetImportContext ctx)
        {
            objectNames.Clear();
            
            HashSet<AttributeInfo> attributeInfoSet = new();

            using var stream = File.Open(ctx.assetPath, FileMode.Open);
            using var reader = new BinaryReader(stream);
            DataParser parser = new(reader);

            parser.ReadHeader();

            for (int objectIndex = 0; objectIndex < parser.NumObjects; ++objectIndex)
            {
                parser.ReadObjectHeader();

                objectNames.Add(parser.ObjectName);

                for (int recordIndex = 0; recordIndex < parser.NumRecords; ++recordIndex)
                {
                    parser.ReadRecordHeader();

                    attributeInfoSet.Add(new AttributeInfo()
                    {
                        dimensions = parser.Dimensions,
                        name = parser.AttributeName
                    });

                    parser.SkipRecordData();
                }
            }

            activeAttributeInfo.Clear();
            activeAttributeInfo.AddRange(attributeInfoSet);

            UpdateConfigs(ctx);

            AttributeMetadata metadata = ScriptableObject.CreateInstance<AttributeMetadata>();
            metadata.Setup(this);

            ctx.AddObjectToAsset("Vertex Metadata", metadata);
            ctx.SetMainObject(metadata);
        }

        void UpdateConfigs(AssetImportContext ctx)
        {
            foreach (var info in activeAttributeInfo)
            {
                AttributeConfig config = attributeConfigs.FirstOrDefault(config => config.name == info.name);

                if (config == null)
                {
                    config = new()
                    {
                        name = info.name,
                        targets = new()
                    };

                    attributeConfigs.Add(config);
                }

                if (config.targets.Count != info.dimensions)
                {
                    while (config.targets.Count > info.dimensions)
                    {
                        config.targets.RemoveAt(config.targets.Count - 1);
                    }

                    for (int i = config.targets.Count; i < info.dimensions; ++i)
                    {
                        AttributeTarget target = new()
                        {
                            kind = AttributeTarget.Kind.UV,
                            uvTarget = new()
                            {
                                channel = UVChannel.UV1,
                                component = (UVComponent)i
                            }
                        };

                        config.targets.Add(target);
                    }
                }
            }

            foreach (var config in attributeConfigs)
            {
                config.exists = activeAttributeInfo.Any(info => info.name == config.name);
            }
        }
    }
}
