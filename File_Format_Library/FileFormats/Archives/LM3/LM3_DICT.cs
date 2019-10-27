﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox;
using System.Windows.Forms;
using Toolbox.Library;
using Toolbox.Library.IO;
using Toolbox.Library.Forms;
using System.Drawing;

namespace FirstPlugin.LuigisMansion3
{
    //Parse info based on https://github.com/TheFearsomeDzeraora/LM3L
    public class LM3_DICT : TreeNodeFile, IFileFormat
    {
        public FileType FileType { get; set; } = FileType.Archive;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "Luigi's Mansion 3 Dictionary" };
        public string[] Extension { get; set; } = new string[] { "*.dict" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool CanAddFiles { get; set; }
        public bool CanRenameFiles { get; set; }
        public bool CanReplaceFiles { get; set; }
        public bool CanDeleteFiles { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
            {
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;
                if (reader.ReadUInt32() == 0x5824F3A9)
                {
                    //This value seems consistant enough to tell apart from LM3
                    reader.SeekBegin(12);
                    return reader.ReadUInt32() == 0x78340300;
                }

                return false;
            }
        }

        public Type[] Types
        {
            get
            {
                List<Type> types = new List<Type>();
                return types.ToArray();
            }
        }

        public override void OnAfterAdded()
        {
            if (!DrawablesLoaded)
            {
                ObjectEditor.AddContainer(DrawableContainer);
                DrawablesLoaded = true;
            }
        }

        public static bool DebugMode = false;

        public List<ChunkDataEntry> chunkEntries = new List<ChunkDataEntry>();

        public bool IsCompressed = false;

        public LM3_ChunkTable ChunkTable;
        public List<FileEntry> fileEntries = new List<FileEntry>();

        public LM3_Renderer Renderer;
        public DrawableContainer DrawableContainer = new DrawableContainer();

        STTextureFolder textureFolder = new STTextureFolder("Textures");
        LM3_ModelFolder modelFolder;
        TreeNode materialNamesFolder = new TreeNode("Material Names");
        TreeNode chunkFolder = new TreeNode("Chunks");

        public static Dictionary<uint, string> HashNames = new Dictionary<uint, string>();

        private void LoadHashes()
        {
         /*   foreach (string hashStr in Properties.Resources.LM3_Hashes.Split('\n'))
            {
                uint hash = Toolbox.Library.Security.Cryptography.Crc32.Compute(hashStr);
                if (!HashNames.ContainsKey(hash))
                    HashNames.Add(hash, hashStr);

                foreach (string pathStr in hashStr.Split('/'))
                {
                    uint hash2 = Toolbox.Library.Security.Cryptography.Crc32.Compute(pathStr);
                    if (!HashNames.ContainsKey(hash2))
                        HashNames.Add(hash2, pathStr);
                }
            }*/
        }

        public byte[] GetFileVertexData()
        {
           return fileEntries[60].GetData(); //Get the fourth file
        }

        public bool DrawablesLoaded = false;
        public void Load(System.IO.Stream stream)
        {
            LoadHashes();
            modelFolder = new LM3_ModelFolder(this); 
            DrawableContainer.Name = FileName;
            Renderer = new LM3_Renderer();
            DrawableContainer.Drawables.Add(Renderer);
            
            Text = FileName;

            using (var reader = new FileReader(stream))
            {
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;
                uint Identifier = reader.ReadUInt32();
                ushort Unknown = reader.ReadUInt16(); //Could also be 2 bytes, not sure. Always 0x0401
                IsCompressed = reader.ReadByte() == 1;
                reader.ReadByte(); //Padding
                uint unk = reader.ReadUInt32();
                uint unk2 = reader.ReadUInt32();

                //Start of the chunk info. A fixed list of chunk information

                TreeNode chunkNodes = new TreeNode("Chunks Debug");

                for (int i = 0; i < 52; i++)
                {
                    ChunkInfo chunk = new ChunkInfo();
                    chunk.Read(reader);
                    chunkNodes.Nodes.Add(chunk);
                }

                TreeNode tableNodes = new TreeNode("File Section Entries");

                if (DebugMode)
                    Nodes.Add(chunkNodes);

                Nodes.Add(tableNodes);

                var FileCount = 120;

                long FileTablePos = reader.Position;
                for (int i = 0; i < FileCount; i++)
                {
                    var file = new FileEntry(this);
                    file.Read(reader);
                    fileEntries.Add(file);

                    if (file.DecompressedSize > 0)
                    {
                        file.Text = $"entry {i}";
                        tableNodes.Nodes.Add(file);
                    }

                    //The first file stores a chunk layout
                    //The second one seems to be a duplicate? 
                    if (i == 0)
                    {
                        using (var tableReader = new FileReader(file.GetData()))
                        {
                            ChunkTable = new LM3_ChunkTable();
                            ChunkTable.Read(tableReader);

                            if (DebugMode)
                            {
                                TreeNode debugFolder = new TreeNode("DEBUG TABLE INFO");
                                Nodes.Add(debugFolder);

                                TreeNode list1 = new TreeNode("Entry List 1");
                                TreeNode list2 = new TreeNode("Entry List 2 ");
                                debugFolder.Nodes.Add(list1);
                                debugFolder.Nodes.Add(list2);
                                debugFolder.Nodes.Add(chunkFolder);

                                foreach (var chunk in ChunkTable.ChunkEntries)
                                {
                                    list1.Nodes.Add($"ChunkType {chunk.ChunkType} ChunkOffset {chunk.ChunkOffset}  Unknown1 {chunk.Unknown1}  ChunkSubCount {chunk.ChunkSubCount}  Unknown3 {chunk.Unknown3}");
                                }
                                foreach (var chunk in ChunkTable.ChunkSubEntries)
                                {
                                    list2.Nodes.Add($"ChunkType 0x{chunk.ChunkType.ToString("X")} Size {chunk.ChunkSize}   Offset {chunk.ChunkOffset}");
                                }
                            }
                        }
                    }
                }


                //Model data block
                //Contains texture hash refs and model headers
                byte[] File052Data = fileEntries[52].GetData();

                //Contains model data
                byte[] File054Data = fileEntries[54].GetData();

                //Image header block
                byte[] File063Data = fileEntries[63].GetData();

                //Image data block
                byte[] File065Data = fileEntries[65].GetData();

                //Set an instance of our current data
                //Chunks are in order, so you build off of when an instance gets loaded
                LM3_Model currentModel = new LM3_Model(this);

                TexturePOWE currentTexture = new TexturePOWE();

                int chunkId = 0;
                uint modelIndex = 0;
                uint ImageHeaderIndex = 0;
                foreach (var chunk in ChunkTable.ChunkSubEntries)
                {
                    var chunkEntry = new ChunkDataEntry(this, chunk);
                    switch (chunk.ChunkType)
                    {
                        case SubDataType.TextureHeader:
                            chunkEntry.DataFile = File063Data;

                            //Read the info
                            using (var textureReader = new FileReader(chunkEntry.FileData))
                            {
                                currentTexture = new TexturePOWE();
                                currentTexture.ImageKey = "texture";
                                currentTexture.SelectedImageKey = currentTexture.ImageKey;
                                currentTexture.Index = ImageHeaderIndex;
                                currentTexture.Read(textureReader);
                                if (DebugMode)
                                    currentTexture.Text = $"Texture {ImageHeaderIndex} {currentTexture.TexFormat.ToString("X")} {currentTexture.Unknown.ToString("X")}";
                                else
                                    currentTexture.Text = $"Texture {currentTexture.ID2.ToString("X")}";

                                if (HashNames.ContainsKey(currentTexture.ID2))
                                    currentTexture.Text = HashNames[currentTexture.ID2];
                                textureFolder.Nodes.Add(currentTexture);
                                Renderer.TextureList.Add(currentTexture);

                                ImageHeaderIndex++;
                            }
                            break;
                        case SubDataType.TextureData:
                            chunkEntry.DataFile = File065Data;
                            currentTexture.ImageData = chunkEntry.FileData;
                            break;
                    /*    case SubDataType.ModelStart:
                            chunkEntry.DataFile = File052Data;
                            currentModel = new LM3_Model(this);
                            currentModel.ModelInfo = new LM3_ModelInfo();
                            currentModel.Text = $"Model {modelIndex}";
                            currentModel.ModelInfo.Data = chunkEntry.FileData;
                            modelFolder.Nodes.Add(currentModel);
                            modelIndex++;
                            break;
                        case SubDataType.MeshBuffers:
                            chunkEntry.DataFile = File054Data;
                            currentModel.BufferStart = chunkEntry.Entry.ChunkOffset;
                            currentModel.BufferSize = chunkEntry.Entry.ChunkSize;
                            break;
                        case SubDataType.VertexStartPointers:
                            chunkEntry.DataFile = File052Data;
                            using (var vtxPtrReader = new FileReader(chunkEntry.FileData))
                            {
                                while (!vtxPtrReader.EndOfStream)
                                    currentModel.VertexBufferPointers.Add(vtxPtrReader.ReadUInt32());
                            }
                            break;
                        case SubDataType.SubmeshInfo:
                            chunkEntry.DataFile = File052Data;
                            int MeshCount = chunkEntry.FileData.Length / 0x28;
                            using (var meshReader = new FileReader(chunkEntry.FileData))
                            {
                                for (uint i = 0; i < MeshCount; i++)
                                {
                                    LM3_Mesh mesh = new LM3_Mesh();
                                    mesh.Read(meshReader);
                                    currentModel.Meshes.Add(mesh);
                                }
                            }
                            currentModel.ModelInfo.Read(new FileReader(currentModel.ModelInfo.Data), currentModel.Meshes);
                            break;
                        case SubDataType.ModelTransform:
                            chunkEntry.DataFile = File052Data;
                            using (var transformReader = new FileReader(chunkEntry.FileData))
                            {
                                //This is possibly very wrong
                                //The data isn't always per mesh, but sometimes is
                                if (transformReader.BaseStream.Length / 0x40 == currentModel.Meshes.Count)
                                {
                                    for (int i = 0; i < currentModel.Meshes.Count; i++)
                                        currentModel.Meshes[i].Transform = transformReader.ReadMatrix4();
                                }
                            }
                            break;
                        case SubDataType.MaterialName:
                               using (var matReader = new FileReader(chunkEntry.FileData))
                               {
                                   materialNamesFolder.Nodes.Add(matReader.ReadZeroTerminatedString());
                               }
                            break;*/
                        default:
                            chunkEntry.DataFile = File052Data;
                            break;
                    }

                    chunkEntry.Text = $"{chunk.ChunkType.ToString("X")} {chunk.ChunkType} {chunk.ChunkOffset} {chunk.ChunkSize}";
                    chunkFolder.Nodes.Add(chunkEntry);
                }

                if (textureFolder.Nodes.Count > 0)
                    Nodes.Add(textureFolder);

                foreach (LM3_Model model in modelFolder.Nodes)
                {
                    model.ReadVertexBuffers();
                }

                if (modelFolder.Nodes.Count > 0)
                    Nodes.Add(modelFolder);
            }
        }

        public void Unload()
        {

        }

        public void Save(System.IO.Stream stream)
        {
        }

        public bool AddFile(ArchiveFileInfo archiveFileInfo)
        {
            return false;
        }

        public bool DeleteFile(ArchiveFileInfo archiveFileInfo)
        {
            return false;
        }

        public class ChunkInfo : TreeNodeCustom
        {
            public string Type;

            public void Read(FileReader reader)
            {
                uint Unknown1 = reader.ReadUInt32();
                ushort Unknown2 = reader.ReadUInt16();
                ushort Unknown3 = reader.ReadUInt16();
                uint Unknown4 = reader.ReadUInt32();
                Type = reader.ReadString(3);
                byte Unknown5 = reader.ReadByte();
                uint Unknown6 = reader.ReadUInt32();
                uint Unknown7 = reader.ReadUInt32();

                Text = $" Type: [{Type}]  [{Unknown1} {Unknown2} {Unknown3} {Unknown4} {Unknown5} {Unknown6}] ";
            }
        }

        public class ChunkDataEntry : TreeNodeFile, IContextMenuNode
        {
            public byte[] DataFile;
            public LM3_DICT ParentDictionary { get; set; }
            public ChunkSubEntry Entry;

            public ChunkDataEntry(LM3_DICT dict, ChunkSubEntry entry)
            {
                ParentDictionary = dict;
                Entry = entry;
            }

            public byte[] FileData
            {
                get
                {
                    using (var reader = new FileReader(DataFile))
                    {
                        reader.SeekBegin(Entry.ChunkOffset);
                        return reader.ReadBytes((int)Entry.ChunkSize);
                    }
                }
            }

            public ToolStripItem[] GetContextMenuItems()
            {
                List<ToolStripItem> Items = new List<ToolStripItem>();
                Items.Add(new STToolStipMenuItem("Export Raw Data", null, Export, Keys.Control | Keys.E));
                return Items.ToArray();
            }

            private void Export(object sender, EventArgs args)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = Text;
                sfd.Filter = "Raw Data (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    System.IO.File.WriteAllBytes(sfd.FileName, FileData);
                }
            }

            public override void OnClick(TreeView treeView)
            {
                HexEditor editor = (HexEditor)LibraryGUI.GetActiveContent(typeof(HexEditor));
                if (editor == null)
                {
                    editor = new HexEditor();
                    LibraryGUI.LoadEditor(editor);
                }
                editor.Text = Text;
                editor.Dock = DockStyle.Fill;
                editor.LoadData(FileData);
            }
        }

        public class FileEntry : TreeNodeFile, IContextMenuNode
        {
            public LM3_DICT ParentDictionary { get; set; }

            public uint Offset;
            public uint DecompressedSize;
            public uint CompressedSize;
            public ushort Unknown1; 
            public byte Unknown2;
            public byte Unknown3; //Possibly the effect? 0 for image block, 1 for info

            public FileEntry(LM3_DICT dict)
            {
                ParentDictionary = dict;
            }

            public void Read(FileReader reader)
            {
                Offset = reader.ReadUInt32();
                DecompressedSize = reader.ReadUInt32();
                CompressedSize = reader.ReadUInt32();
                Unknown1 = reader.ReadUInt16();
                Unknown2 = reader.ReadByte();
                Unknown3 = reader.ReadByte();
            }

            private bool IsTextureBinary()
            {
                byte[] Data = GetData();

                if (Data.Length < 4)
                    return false;

                using (var reader = new FileReader(Data))
                {
                    return reader.ReadUInt32() == 0xE977D350;
                }
            }

            public ToolStripItem[] GetContextMenuItems()
            {
                List<ToolStripItem> Items = new List<ToolStripItem>();
                Items.Add(new STToolStipMenuItem("Export Raw Data", null, Export, Keys.Control | Keys.E));
                return Items.ToArray();
            }

            private void Export(object sender, EventArgs args)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = Text;
                sfd.Filter = "Raw Data (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    System.IO.File.WriteAllBytes(sfd.FileName, GetData());
                }
            }

            public override void OnClick(TreeView treeView)
            {
                HexEditor editor = (HexEditor)LibraryGUI.GetActiveContent(typeof(HexEditor));
                if (editor == null)
                {
                    editor = new HexEditor();
                    LibraryGUI.LoadEditor(editor);
                }
                editor.Text = Text;
                editor.Dock = DockStyle.Fill;
                editor.LoadData(GetData());
            }

            public byte[] GetData()
            {
                byte[] Data = new byte[DecompressedSize];

                string FolderPath = System.IO.Path.GetDirectoryName(ParentDictionary.FilePath);
                string DataFile = System.IO.Path.Combine(FolderPath, $"{ParentDictionary.FileName.Replace(".dict", ".data")}");

                if (System.IO.File.Exists(DataFile))
                {
                    using (var reader = new FileReader(DataFile))
                    {
                        if (Offset > reader.BaseStream.Length)
                            return reader.ReadBytes((int)CompressedSize);

                        reader.SeekBegin(Offset);
                        if (ParentDictionary.IsCompressed)
                        {
                            ushort Magic = reader.ReadUInt16();
                            reader.SeekBegin(Offset);

                            Data = reader.ReadBytes((int)CompressedSize);
                            if (Magic == 0x9C78 || Magic == 0xDA78)
                                return STLibraryCompression.ZLIB.Decompress(Data);
                            else //Unknown compression 
                                return Data;
                        }
                        else
                        {
                            return reader.ReadBytes((int)DecompressedSize);
                        }
                    }
                }

                return Data;
            }
        }
    }
}
