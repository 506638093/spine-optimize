/******************************************************************************
 * Spine Runtimes Software License
 * Version 2.1
 * 
 * Copyright (c) 2013, Esoteric Software
 * All rights reserved.
 * 
 * You are granted a perpetual, non-exclusive, non-sublicensable and
 * non-transferable license to install, execute and perform the Spine Runtimes
 * Software (the "Software") solely for internal use. Without the written
 * permission of Esoteric Software (typically granted by licensing Spine), you
 * may not (a) modify, translate, adapt or otherwise create derivative works,
 * improvements of the Software or develop new applications using the Software
 * or (b) remove, delete, alter or obscure any trademarks or any copyright,
 * trademark, patent or other intellectual property or proprietary rights
 * notices on or in the Software, including any copy thereof. Redistributions
 * in binary or source form must include this license and terms.
 * 
 * THIS SOFTWARE IS PROVIDED BY ESOTERIC SOFTWARE "AS IS" AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
 * EVENT SHALL ESOTERIC SOFTARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
 * OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
 * OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *****************************************************************************/
#define OPTIMIZE_SPINE_READ //HuaHua
using System;
using System.IO;
using System.Collections.Generic;

#if WINDOWS_STOREAPP
using System.Threading.Tasks;
using Windows.Storage;
#endif

namespace Spine {
	public class SkeletonBinary {
		public const int TIMELINE_SCALE = 0;
		public const int TIMELINE_ROTATE = 1;
		public const int TIMELINE_TRANSLATE = 2;
		public const int TIMELINE_ATTACHMENT = 3;
		public const int TIMELINE_COLOR = 4;
		public const int TIMELINE_FLIPX = 5;
		public const int TIMELINE_FLIPY = 6;

		public const int CURVE_LINEAR = 0;
		public const int CURVE_STEPPED = 1;
		public const int CURVE_BEZIER = 2;

		private AttachmentLoader attachmentLoader;
		public float Scale { get; set; }
		private char[] chars = new char[32];
		private byte[] buffer = new byte[4];

		public SkeletonBinary (params Atlas[] atlasArray)
			: this(new AtlasAttachmentLoader(atlasArray)) {
		}

		public SkeletonBinary (AttachmentLoader attachmentLoader) {
			if (attachmentLoader == null) throw new ArgumentNullException("attachmentLoader cannot be null.");
			this.attachmentLoader = attachmentLoader;
			Scale = 1;
		}

#if WINDOWS_STOREAPP
		private async Task<SkeletonData> ReadFile(string path) {
			var folder = Windows.ApplicationModel.Package.Current.InstalledLocation;
			using (var input = new BufferedStream(await folder.GetFileAsync(path).AsTask().ConfigureAwait(false))) {
				SkeletonData skeletonData = ReadSkeletonData(input);
				skeletonData.Name = Path.GetFileNameWithoutExtension(path);
				return skeletonData;
			}
		}

		public SkeletonData ReadSkeletonData (String path) {
			return this.ReadFile(path).Result;
		}
#else
		public SkeletonData ReadSkeletonData (String path) {
#if WINDOWS_PHONE
			using (var input = new BufferedStream(Microsoft.Xna.Framework.TitleContainer.OpenStream(path)))
			{
#else
			using (var input = new BufferedStream(new FileStream(path, FileMode.Open))) {
#endif
				SkeletonData skeletonData = ReadSkeletonData(input);
				skeletonData.name = Path.GetFileNameWithoutExtension(path);
				return skeletonData;
			}
		}
#endif

#if OPTIMIZE_SPINE_READ

        public List<string> CacheStrings;
        public bool IsOptimizedMode = false;

        public SkeletonData ReadSkeletonData(Stream input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input cannot be null.");
            }

            var binBuffer = new byte[input.Length];
            input.Read(binBuffer, 0, (int)input.Length);
            int binPosition = 0;

            float scale = Scale;

            var skeletonData = new SkeletonData();

            skeletonData.hash = ReadString(binBuffer, ref binPosition, false);
            if (skeletonData.hash.Length == 0) skeletonData.hash = null;
            skeletonData.version = ReadString(binBuffer, ref binPosition, false);
            if (skeletonData.version.Length == 0) skeletonData.version = null;
            skeletonData.width = ReadFloat(binBuffer, ref binPosition);
            skeletonData.height = ReadFloat(binBuffer, ref binPosition);
            bool nonessential = ReadBoolean(binBuffer, ref binPosition);
            if (nonessential)
            {
                ReadString(binBuffer, ref binPosition);
            }

            if (skeletonData.version == "HuaHua1.0")
            {
                IsOptimizedMode = true;
            }

            //Cache Mode
            if (IsOptimizedMode)
            {
                var count = ReadInt(binBuffer, ref binPosition, true);
                if (CacheStrings == null)
                {
                    CacheStrings = new List<string>(count);
                }
                else
                {
                    CacheStrings.Clear();
                    CacheStrings.Capacity = count;
                }

                for (int i = 0; i < count; ++i)
                {
                    CacheStrings.Add( ReadString(binBuffer, ref binPosition, false) );
                }
            }


            // Bones.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
            {
                String name = ReadString(binBuffer, ref binPosition);
                BoneData parent = null;
                int parentIndex = ReadInt(binBuffer, ref binPosition, true) - 1;
                if (parentIndex != -1) parent = skeletonData.bones[parentIndex];
                BoneData boneData = new BoneData(name, parent);
                boneData.x = ReadFloat(binBuffer, ref binPosition) * scale;
                boneData.y = ReadFloat(binBuffer, ref binPosition) * scale;
                boneData.scaleX = ReadFloat(binBuffer, ref binPosition);
                boneData.scaleY = ReadFloat(binBuffer, ref binPosition);
                boneData.rotation = ReadFloat(binBuffer, ref binPosition);
                boneData.length = ReadFloat(binBuffer, ref binPosition) * scale;
                boneData.flipX = ReadBoolean(binBuffer, ref binPosition);
                boneData.flipY = ReadBoolean(binBuffer, ref binPosition);
                boneData.inheritScale = ReadBoolean(binBuffer, ref binPosition);
                boneData.inheritRotation = ReadBoolean(binBuffer, ref binPosition);
                if (nonessential)
                {
                    ReadInt(binBuffer, ref binPosition); // Skip bone color.
                }
                skeletonData.bones.Add(boneData);
            }

            // IK constraints.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
            {
                IkConstraintData ikConstraintData = new IkConstraintData(ReadString(binBuffer, ref binPosition));
                for (int ii = 0, nn = ReadInt(binBuffer, ref binPosition, true); ii < nn; ii++)
                    ikConstraintData.bones.Add(skeletonData.bones[ReadInt(binBuffer, ref binPosition, true)]);
                ikConstraintData.target = skeletonData.bones[ReadInt(binBuffer, ref binPosition, true)];
                ikConstraintData.mix = ReadFloat(binBuffer, ref binPosition);
                ikConstraintData.bendDirection = ReadSByte(binBuffer, ref binPosition);
                skeletonData.ikConstraints.Add(ikConstraintData);
            }
            
            // Slots.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
            {
                String slotName = ReadString(binBuffer, ref binPosition);
                BoneData boneData = skeletonData.bones[ReadInt(binBuffer, ref binPosition, true)];
                SlotData slotData = new SlotData(slotName, boneData);
                int color = ReadInt(binBuffer, ref binPosition);
                slotData.r = ((color & 0xff000000) >> 24) / 255f;
                slotData.g = ((color & 0x00ff0000) >> 16) / 255f;
                slotData.b = ((color & 0x0000ff00) >> 8) / 255f;
                slotData.a = ((color & 0x000000ff)) / 255f;
                slotData.attachmentName = ReadString(binBuffer, ref binPosition);
                slotData.additiveBlending = ReadBoolean(binBuffer, ref binPosition);
                skeletonData.slots.Add(slotData);
            }

            // Default skin.
            Skin defaultSkin = ReadSkin(binBuffer, ref binPosition, "default", nonessential);
            if (defaultSkin != null)
            {
                skeletonData.defaultSkin = defaultSkin;
                skeletonData.skins.Add(defaultSkin);
            }

            // Skins.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
                skeletonData.skins.Add(ReadSkin(binBuffer, ref binPosition, ReadString(binBuffer, ref binPosition), nonessential));

            // Events.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
            {
                EventData eventData = new EventData(ReadString(binBuffer, ref binPosition));
                eventData.Int = ReadInt(binBuffer, ref binPosition, false);
                eventData.Float = ReadFloat(binBuffer, ref binPosition);
                eventData.String = ReadString(binBuffer, ref binPosition);
                skeletonData.events.Add(eventData);
            }

            // Animations.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
                ReadAnimation(ReadString(binBuffer, ref binPosition), binBuffer, ref binPosition, skeletonData);

            skeletonData.bones.TrimExcess();
            skeletonData.slots.TrimExcess();
            skeletonData.skins.TrimExcess();
            skeletonData.events.TrimExcess();
            skeletonData.animations.TrimExcess();
            skeletonData.ikConstraints.TrimExcess();
            return skeletonData;
        }

        private static int ReadVarint(Stream input, bool optimizePositive)
        {
            int b = input.ReadByte();
            int result = b & 0x7F;
            if ((b & 0x80) != 0)
            {
                b = input.ReadByte();
                result |= (b & 0x7F) << 7;
                if ((b & 0x80) != 0)
                {
                    b = input.ReadByte();
                    result |= (b & 0x7F) << 14;
                    if ((b & 0x80) != 0)
                    {
                        b = input.ReadByte();
                        result |= (b & 0x7F) << 21;
                        if ((b & 0x80) != 0) result |= (input.ReadByte() & 0x7F) << 28;
                    }
                }
            }
            return optimizePositive ? result : ((result >> 1) ^ -(result & 1));
        }

        private static void ReadFully(Stream input, byte[] buffer, int offset, int length)
        {
            while (length > 0)
            {
                int count = input.Read(buffer, offset, length);
                if (count <= 0) throw new EndOfStreamException();
                offset += count;
                length -= count;
            }
        }

        public static string GetVersionString(Stream input)
        {
            if (input == null) throw new ArgumentNullException("input");

            try
            {
                // Hash.
                int byteCount = ReadVarint(input, true);
                if (byteCount > 1) input.Position += byteCount - 1;

                // Version.
                byteCount = ReadVarint(input, true);
                if (byteCount > 1)
                {
                    byteCount--;
                    var buffer = new byte[byteCount];
                    ReadFully(input, buffer, 0, byteCount);
                    return System.Text.Encoding.UTF8.GetString(buffer, 0, byteCount);
                }

                throw new ArgumentException("Stream does not contain a valid binary Skeleton Data.", "input");
            }
            catch (Exception e)
            {
                throw new ArgumentException("Stream does not contain a valid binary Skeleton Data.\n" + e, "input");
            }
        }


        /** @return May be null. */
        private Skin ReadSkin(byte[] binBuffer, ref int binPosition, String skinName, bool nonessential)
        {
            int slotCount = ReadInt(binBuffer, ref binPosition, true);
            if (slotCount == 0)
            {
                return null;
            }
            Skin skin = new Skin(skinName);
            for (int i = 0; i < slotCount; i++)
            {
                int slotIndex = ReadInt(binBuffer, ref binPosition, true);
                for (int ii = 0, nn = ReadInt(binBuffer, ref binPosition, true); ii < nn; ii++)
                {
                    String name = ReadString(binBuffer, ref binPosition);
                    skin.AddAttachment(slotIndex, name, ReadAttachment(binBuffer, ref binPosition, skin, name, nonessential));
                }
            }
            return skin;
        }

        private Attachment ReadAttachment(byte[] binBuffer, ref int binPosition, Skin skin, String attachmentName, bool nonessential)
        {
            float scale = Scale;

            String name = ReadString(binBuffer, ref binPosition);
            if (name == null) name = attachmentName;

            switch ((AttachmentType)binBuffer[binPosition++])
            {
                case AttachmentType.region:
                    {
                        String path = ReadString(binBuffer, ref binPosition);
                        if (path == null) path = name;
                        RegionAttachment region = attachmentLoader.NewRegionAttachment(skin, name, path);
                        if (region == null) return null;
                        region.Path = path;
                        region.x = ReadFloat(binBuffer, ref binPosition) * scale;
                        region.y = ReadFloat(binBuffer, ref binPosition) * scale;
                        region.scaleX = ReadFloat(binBuffer, ref binPosition);
                        region.scaleY = ReadFloat(binBuffer, ref binPosition);
                        region.rotation = ReadFloat(binBuffer, ref binPosition);
                        region.width = ReadFloat(binBuffer, ref binPosition) * scale;
                        region.height = ReadFloat(binBuffer, ref binPosition) * scale;
                        int color = ReadInt(binBuffer, ref binPosition);
                        region.r = ((color & 0xff000000) >> 24) / 255f;
                        region.g = ((color & 0x00ff0000) >> 16) / 255f;
                        region.b = ((color & 0x0000ff00) >> 8) / 255f;
                        region.a = ((color & 0x000000ff)) / 255f;
                        region.UpdateOffset();
                        return region;
                    }
                case AttachmentType.boundingbox:
                    {
                        BoundingBoxAttachment box = attachmentLoader.NewBoundingBoxAttachment(skin, name);
                        if (box == null) return null;
                        box.vertices = ReadFloatArray(binBuffer, ref binPosition, scale);
                        return box;
                    }
                case AttachmentType.mesh:
                    {
                        String path = ReadString(binBuffer, ref binPosition);
                        if (path == null) path = name;
                        MeshAttachment mesh = attachmentLoader.NewMeshAttachment(skin, name, path);
                        if (mesh == null) return null;
                        mesh.Path = path;
                        mesh.regionUVs = ReadFloatArray(binBuffer, ref binPosition, 1);
                        mesh.triangles = ReadShortArray(binBuffer, ref binPosition);
                        mesh.vertices = ReadFloatArray(binBuffer, ref binPosition, scale);
                        mesh.UpdateUVs();
                        int color = ReadInt(binBuffer, ref binPosition);
                        mesh.r = ((color & 0xff000000) >> 24) / 255f;
                        mesh.g = ((color & 0x00ff0000) >> 16) / 255f;
                        mesh.b = ((color & 0x0000ff00) >> 8) / 255f;
                        mesh.a = ((color & 0x000000ff)) / 255f;
                        mesh.HullLength = ReadInt(binBuffer, ref binPosition, true) * 2;
                        if (nonessential)
                        {
                            ReadIntArray(binBuffer, ref binPosition);
                            ReadFloat(binBuffer, ref binPosition);
                            ReadFloat(binBuffer, ref binPosition);
                        }
                        return mesh;
                    }
                case AttachmentType.skinnedmesh:
                    {
                        String path = ReadString(binBuffer, ref binPosition);
                        if (path == null) path = name;
                        SkinnedMeshAttachment mesh = attachmentLoader.NewSkinnedMeshAttachment(skin, name, path);
                        if (mesh == null) return null;
                        mesh.Path = path;
                        float[] uvs = ReadFloatArray(binBuffer, ref binPosition, 1);
                        int[] triangles = ReadShortArray(binBuffer, ref binPosition);

                        int vertexCount = ReadInt(binBuffer, ref binPosition, true);
                        var weights = new List<float>(uvs.Length * 3 * 3);
                        var bones = new List<int>(uvs.Length * 3);
                        for (int i = 0; i < vertexCount; i++)
                        {
                            int boneCount = (int)ReadFloat(binBuffer, ref binPosition);
                            bones.Add(boneCount);
                            for (int nn = i + boneCount * 4; i < nn; i += 4)
                            {
                                bones.Add((int)ReadFloat(binBuffer, ref binPosition));
                                weights.Add(ReadFloat(binBuffer, ref binPosition) * scale);
                                weights.Add(ReadFloat(binBuffer, ref binPosition) * scale);
                                weights.Add(ReadFloat(binBuffer, ref binPosition));
                            }
                        }
                        mesh.bones = bones.ToArray();
                        mesh.weights = weights.ToArray();
                        mesh.triangles = triangles;
                        mesh.regionUVs = uvs;
                        mesh.UpdateUVs();
                        int color = ReadInt(binBuffer, ref binPosition);
                        mesh.r = ((color & 0xff000000) >> 24) / 255f;
                        mesh.g = ((color & 0x00ff0000) >> 16) / 255f;
                        mesh.b = ((color & 0x0000ff00) >> 8) / 255f;
                        mesh.a = ((color & 0x000000ff)) / 255f;
                        mesh.HullLength = ReadInt(binBuffer, ref binPosition, true) * 2;
                        if (nonessential)
                        {
                            ReadIntArray(binBuffer, ref binPosition);
                            ReadFloat(binBuffer, ref binPosition);
                            ReadFloat(binBuffer, ref binPosition);
                        }
                        return mesh;
                    }
            }
            return null;
        }

        private float[] ReadFloatArray(byte[] binBuffer, ref int binPosition, float scale)
        {
            int n = ReadInt(binBuffer, ref binPosition, true);
            float[] array = new float[n];
            if (scale == 1)
            {
                for (int i = 0; i < n; i++)
                    array[i] = ReadFloat(binBuffer, ref binPosition);
            }
            else
            {
                for (int i = 0; i < n; i++)
                    array[i] = ReadFloat(binBuffer, ref binPosition) * scale;
            }
            return array;
        }

        private int[] ReadShortArray(byte[] binBuffer, ref int binPosition)
        {
            int n = ReadInt(binBuffer, ref binPosition, true);
            int[] array = new int[n];
            for (int i = 0; i < n; i++)
                array[i] = (binBuffer[binPosition++] << 8) + binBuffer[binPosition++];
            return array;
        }

        private int[] ReadIntArray(byte[] binBuffer, ref int binPosition)
        {
            int n = ReadInt(binBuffer, ref binPosition, true);
            int[] array = new int[n];
            for (int i = 0; i < n; i++)
                array[i] = ReadInt(binBuffer, ref binPosition, true);
            return array;
        }

        private void ReadAnimation(String name, byte[] binBuffer, ref int binPosition, SkeletonData skeletonData)
        {
            var timelines = new List<Timeline>();
            float scale = Scale;
            float duration = 0;

            // Slot timelines.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
            {
                int slotIndex = ReadInt(binBuffer, ref binPosition, true);
                for (int ii = 0, nn = ReadInt(binBuffer, ref binPosition, true); ii < nn; ii++)
                {
                    int timelineType = binBuffer[binPosition++];
                    int frameCount = ReadInt(binBuffer, ref binPosition, true);
                    switch (timelineType)
                    {
                        case TIMELINE_COLOR:
                            {
                                ColorTimeline timeline = new ColorTimeline(frameCount);
                                timeline.slotIndex = slotIndex;
                                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                                {
                                    float time = ReadFloat(binBuffer, ref binPosition);
                                    int color = ReadInt(binBuffer, ref binPosition);
                                    float r = ((color & 0xff000000) >> 24) / 255f;
                                    float g = ((color & 0x00ff0000) >> 16) / 255f;
                                    float b = ((color & 0x0000ff00) >> 8) / 255f;
                                    float a = ((color & 0x000000ff)) / 255f;
                                    timeline.SetFrame(frameIndex, time, r, g, b, a);
                                    if (frameIndex < frameCount - 1)
                                    {
                                        ReadCurve(binBuffer, ref binPosition, frameIndex, timeline);
                                    }
                                }
                                timelines.Add(timeline);
                                duration = Math.Max(duration, timeline.frames[frameCount * 5 - 5]);
                                break;
                            }
                        case TIMELINE_ATTACHMENT:
                            {
                                AttachmentTimeline timeline = new AttachmentTimeline(frameCount);
                                timeline.slotIndex = slotIndex;
                                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                                    timeline.SetFrame(frameIndex, ReadFloat(binBuffer, ref binPosition), ReadString(binBuffer, ref binPosition));
                                timelines.Add(timeline);
                                duration = Math.Max(duration, timeline.frames[frameCount - 1]);
                                break;
                            }
                    }
                }
            }

            // Bone timelines.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
            {
                int boneIndex = ReadInt(binBuffer, ref binPosition, true);
                for (int ii = 0, nn = ReadInt(binBuffer, ref binPosition, true); ii < nn; ii++)
                {
                    int timelineType = binBuffer[binPosition++];
                    int frameCount = ReadInt(binBuffer, ref binPosition, true);
                    switch (timelineType)
                    {
                        case TIMELINE_ROTATE:
                            {
                                RotateTimeline timeline = new RotateTimeline(frameCount);
                                timeline.boneIndex = boneIndex;
                                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                                {
                                    timeline.SetFrame(frameIndex, ReadFloat(binBuffer, ref binPosition), ReadFloat(binBuffer, ref binPosition));
                                    if (frameIndex < frameCount - 1)
                                    {
                                        ReadCurve(binBuffer, ref binPosition, frameIndex, timeline);
                                    }
                                }
                                timelines.Add(timeline);
                                duration = Math.Max(duration, timeline.frames[frameCount * 2 - 2]);
                                break;
                            }
                        case TIMELINE_TRANSLATE:
                        case TIMELINE_SCALE:
                            {
                                TranslateTimeline timeline;
                                float timelineScale = 1;
                                if (timelineType == TIMELINE_SCALE)
                                {
                                    timeline = new ScaleTimeline(frameCount);
                                }
                                else
                                {
                                    timeline = new TranslateTimeline(frameCount);
                                    timelineScale = scale;
                                }
                                timeline.boneIndex = boneIndex;
                                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                                {
                                    timeline.SetFrame(frameIndex, ReadFloat(binBuffer, ref binPosition), ReadFloat(binBuffer, ref binPosition) * timelineScale, ReadFloat(binBuffer, ref binPosition)
                                        * timelineScale);
                                    if (frameIndex < frameCount - 1)
                                    {
                                        ReadCurve(binBuffer, ref binPosition, frameIndex, timeline);
                                    }
                                }
                                timelines.Add(timeline);
                                duration = Math.Max(duration, timeline.frames[frameCount * 3 - 3]);
                                break;
                            }
                        case TIMELINE_FLIPX:
                        case TIMELINE_FLIPY:
                            {
                                FlipXTimeline timeline = timelineType == TIMELINE_FLIPX ? new FlipXTimeline(frameCount) : new FlipYTimeline(
                                    frameCount);
                                timeline.boneIndex = boneIndex;
                                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                                    timeline.SetFrame(frameIndex, ReadFloat(binBuffer, ref binPosition), ReadBoolean(binBuffer, ref binPosition));
                                timelines.Add(timeline);
                                duration = Math.Max(duration, timeline.frames[frameCount * 2 - 2]);
                                break;
                            }
                    }
                }
            }

            // IK timelines.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
            {
                IkConstraintData ikConstraint = skeletonData.ikConstraints[ReadInt(binBuffer, ref binPosition, true)];
                int frameCount = ReadInt(binBuffer, ref binPosition, true);
                IkConstraintTimeline timeline = new IkConstraintTimeline(frameCount);
                timeline.ikConstraintIndex = skeletonData.ikConstraints.IndexOf(ikConstraint);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    timeline.SetFrame(frameIndex, ReadFloat(binBuffer, ref binPosition), ReadFloat(binBuffer, ref binPosition), ReadSByte(binBuffer, ref binPosition));
                    if (frameIndex < frameCount - 1)
                    {
                        ReadCurve(binBuffer, ref binPosition, frameIndex, timeline);
                    }
                }
                timelines.Add(timeline);
                duration = Math.Max(duration, timeline.frames[frameCount * 3 - 3]);
            }

            // FFD timelines.
            for (int i = 0, n = ReadInt(binBuffer, ref binPosition, true); i < n; i++)
            {
                Skin skin = skeletonData.skins[ReadInt(binBuffer, ref binPosition, true)];
                for (int ii = 0, nn = ReadInt(binBuffer, ref binPosition, true); ii < nn; ii++)
                {
                    int slotIndex = ReadInt(binBuffer, ref binPosition, true);
                    for (int iii = 0, nnn = ReadInt(binBuffer, ref binPosition, true); iii < nnn; iii++)
                    {
                        Attachment attachment = skin.GetAttachment(slotIndex, ReadString(binBuffer, ref binPosition));
                        int frameCount = ReadInt(binBuffer, ref binPosition, true);
                        FFDTimeline timeline = new FFDTimeline(frameCount);
                        timeline.slotIndex = slotIndex;
                        timeline.attachment = attachment;
                        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                        {
                            float time = ReadFloat(binBuffer, ref binPosition);

                            float[] vertices;
                            int vertexCount;
                            if (attachment is MeshAttachment)
                                vertexCount = ((MeshAttachment)attachment).vertices.Length;
                            else
                                vertexCount = ((SkinnedMeshAttachment)attachment).weights.Length / 3 * 2;

                            int end = ReadInt(binBuffer, ref binPosition, true);
                            if (end == 0)
                            {
                                if (attachment is MeshAttachment)
                                    vertices = ((MeshAttachment)attachment).vertices;
                                else
                                    vertices = new float[vertexCount];
                            }
                            else
                            {
                                vertices = new float[vertexCount];
                                int start = ReadInt(binBuffer, ref binPosition, true);
                                end += start;
                                if (scale == 1)
                                {
                                    for (int v = start; v < end; v++)
                                        vertices[v] = ReadFloat(binBuffer, ref binPosition);
                                }
                                else
                                {
                                    for (int v = start; v < end; v++)
                                        vertices[v] = ReadFloat(binBuffer, ref binPosition) * scale;
                                }
                                if (attachment is MeshAttachment)
                                {
                                    float[] meshVertices = ((MeshAttachment)attachment).vertices;
                                    for (int v = 0, vn = vertices.Length; v < vn; v++)
                                        vertices[v] += meshVertices[v];
                                }
                            }

                            timeline.SetFrame(frameIndex, time, vertices);
                            if (frameIndex < frameCount - 1)
                            {
                                ReadCurve(binBuffer, ref binPosition, frameIndex, timeline);
                            }
                        }
                        timelines.Add(timeline);
                        duration = Math.Max(duration, timeline.frames[frameCount - 1]);
                    }
                }
            }

            // Draw order timeline.
            int drawOrderCount = ReadInt(binBuffer, ref binPosition, true);
            if (drawOrderCount > 0)
            {
                DrawOrderTimeline timeline = new DrawOrderTimeline(drawOrderCount);
                int slotCount = skeletonData.slots.Count;
                for (int i = 0; i < drawOrderCount; i++)
                {
                    int offsetCount = ReadInt(binBuffer, ref binPosition, true);
                    int[] drawOrder = new int[slotCount];
                    for (int ii = slotCount - 1; ii >= 0; ii--)
                        drawOrder[ii] = -1;
                    int[] unchanged = new int[slotCount - offsetCount];
                    int originalIndex = 0, unchangedIndex = 0;
                    for (int ii = 0; ii < offsetCount; ii++)
                    {
                        int slotIndex = ReadInt(binBuffer, ref binPosition, true);
                        // Collect unchanged items.
                        while (originalIndex != slotIndex)
                            unchanged[unchangedIndex++] = originalIndex++;
                        // Set changed items.
                        drawOrder[originalIndex + ReadInt(binBuffer, ref binPosition, true)] = originalIndex++;
                    }
                    // Collect remaining unchanged items.
                    while (originalIndex < slotCount)
                        unchanged[unchangedIndex++] = originalIndex++;
                    // Fill in unchanged items.
                    for (int ii = slotCount - 1; ii >= 0; ii--)
                        if (drawOrder[ii] == -1) drawOrder[ii] = unchanged[--unchangedIndex];
                    timeline.SetFrame(i, ReadFloat(binBuffer, ref binPosition), drawOrder);
                }
                timelines.Add(timeline);
                duration = Math.Max(duration, timeline.frames[drawOrderCount - 1]);
            }

            // Event timeline.
            int eventCount = ReadInt(binBuffer, ref binPosition, true);
            if (eventCount > 0)
            {
                EventTimeline timeline = new EventTimeline(eventCount);
                for (int i = 0; i < eventCount; i++)
                {
                    float time = ReadFloat(binBuffer, ref binPosition);
                    EventData eventData = skeletonData.events[ReadInt(binBuffer, ref binPosition, true)];
                    Event e = new Event(eventData);
                    e.Int = ReadInt(binBuffer, ref binPosition, false);
                    e.Float = ReadFloat(binBuffer, ref binPosition);
                    e.String = ReadBoolean(binBuffer, ref binPosition) ? ReadString(binBuffer, ref binPosition) : eventData.String;
                    timeline.SetFrame(i, time, e);
                }
                timelines.Add(timeline);
                duration = Math.Max(duration, timeline.frames[eventCount - 1]);
            }

            timelines.TrimExcess();
            skeletonData.animations.Add(new Animation(name, timelines, duration));
        }

        private void ReadCurve(byte[] binBuffer, ref int binPosition, int frameIndex, CurveTimeline timeline)
        {
            switch (binBuffer[binPosition++])
            {
                case CURVE_STEPPED:
                    timeline.SetStepped(frameIndex);
                    break;
                case CURVE_BEZIER:
                    timeline.SetCurve(frameIndex, ReadFloat(binBuffer, ref binPosition), ReadFloat(binBuffer, ref binPosition), ReadFloat(binBuffer, ref binPosition), ReadFloat(binBuffer, ref binPosition));
                    break;
            }
        }

        private sbyte ReadSByte(byte[] binBuffer, ref int binPosition)
        {
            int value = binBuffer[binPosition++];
            if (value == -1)
            {
                throw new EndOfStreamException();
            }
            return (sbyte)value;
        }

        private bool ReadBoolean(byte[] binBuffer, ref int binPosition)
        {
            return binBuffer[binPosition++] != 0;
        }

        private unsafe float ReadFloat(byte[] binBuffer, ref int binPosition)
        {
            buffer[3] = binBuffer[binPosition++];
            buffer[2] = binBuffer[binPosition++];
            buffer[1] = binBuffer[binPosition++];
            buffer[0] = binBuffer[binPosition++];

            fixed (byte* ptr = &buffer[0])
            {
                return *(float*)ptr;
            }
        }

        private int ReadInt(byte[] binBuffer, ref int binPosition)
        {
            return (binBuffer[binPosition++] << 24) + (binBuffer[binPosition++] << 16) + (binBuffer[binPosition++] << 8) + binBuffer[binPosition++];
        }

        private int ReadInt(byte[] binBuffer, ref int binPosition, bool optimizePositive)
        {
            int b = binBuffer[binPosition++];
            int result = b & 0x7F;
            if ((b & 0x80) != 0)
            {
                b = binBuffer[binPosition++];
                result |= (b & 0x7F) << 7;
                if ((b & 0x80) != 0)
                {
                    b = binBuffer[binPosition++];
                    result |= (b & 0x7F) << 14;
                    if ((b & 0x80) != 0)
                    {
                        b = binBuffer[binPosition++];
                        result |= (b & 0x7F) << 21;
                        if ((b & 0x80) != 0)
                        {
                            b = binBuffer[binPosition++];
                            result |= (b & 0x7F) << 28;
                        }
                    }
                }
            }
            return optimizePositive ? result : ((result >> 1) ^ -(result & 1));
        }

        private string ReadString(byte[] binBuffer, ref int binPosition, bool cacheString = true)
        {
            if (IsOptimizedMode && cacheString)
            {
                var idx = ReadInt(binBuffer, ref binPosition, true);
                return CacheStrings[idx];
            }

            int charCount = ReadInt(binBuffer, ref binPosition, true);
            switch (charCount)
            {
                case 0:
                    return null;
                case 1:
                    return "";
            }
            charCount--;
            char[] chars = this.chars;
            if (chars.Length < charCount)
            {
                this.chars = chars = new char[charCount];
            }
            // Try to read 7 bit ASCII chars.
            int charIndex = 0;
            int b = 0;
            while (charIndex < charCount)
            {
                b = binBuffer[binPosition++];
                if (b > 127)
                    break;
                chars[charIndex++] = (char)b;
            }
            // If a char was not ASCII, finish with slow path.
            if (charIndex < charCount)
            {
                ReadUtf8_slow(binBuffer, ref binPosition, charCount, charIndex, b);
            }
            var ret = new String(chars, 0, charCount);

#if OPTIMIZE_SPINE
            if (cacheString && CacheStrings != null)
            {
                if(!CacheStrings.Contains(ret))
                {
                    CacheStrings.Add(ret);
                }
            }
#endif

            return ret;
        }

        private void ReadUtf8_slow(byte[] binBuffer, ref int binPosition, int charCount, int charIndex, int b)
        {
            char[] chars = this.chars;
            while (true)
            {
                switch (b >> 4)
                {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                    case 6:
                    case 7:
                        chars[charIndex] = (char)b;
                        break;
                    case 12:
                    case 13:
                        chars[charIndex] = (char)((b & 0x1F) << 6 | binBuffer[binPosition++] & 0x3F);
                        break;
                    case 14:
                        chars[charIndex] = (char)((b & 0x0F) << 12 | (binBuffer[binPosition++] & 0x3F) << 6 | binBuffer[binPosition++] & 0x3F);
                        break;
                }
                if (++charIndex >= charCount) 
                    break;
                b = binBuffer[binPosition++] & 0xFF;
            }
        }

        private static int ReadVarint(byte[] binBuffer, ref int binPosition, bool optimizePositive)
        {
            int b = binBuffer[binPosition++];
            int result = b & 0x7F;
            if ((b & 0x80) != 0)
            {
                b = binBuffer[binPosition++];
                result |= (b & 0x7F) << 7;
                if ((b & 0x80) != 0)
                {
                    b = binBuffer[binPosition++];
                    result |= (b & 0x7F) << 14;
                    if ((b & 0x80) != 0)
                    {
                        b = binBuffer[binPosition++];
                        result |= (b & 0x7F) << 21;
                        if ((b & 0x80) != 0) result |= (binBuffer[binPosition++] & 0x7F) << 28;
                    }
                }
            }
            return optimizePositive ? result : ((result >> 1) ^ -(result & 1));
        }


        private static void ReadFully(byte[] inbuffer, ref int binPosition, byte[] binBuffer, int offset, int length)
        {
            Array.Copy(binBuffer, 0, inbuffer, binPosition + offset, length);
            binPosition += length;
        }
#else
        public SkeletonData ReadSkeletonData (Stream input) {
			if (input == null) throw new ArgumentNullException("input cannot be null.");
			float scale = Scale;

			var skeletonData = new SkeletonData();
			skeletonData.hash = ReadString(input);
			if (skeletonData.hash.Length == 0) skeletonData.hash = null;
			skeletonData.version = ReadString(input);
			if (skeletonData.version.Length == 0) skeletonData.version = null;
			skeletonData.width = ReadFloat(input);
			skeletonData.height = ReadFloat(input);

			bool nonessential = ReadBoolean(input);

			if (nonessential) {
				skeletonData.imagesPath = ReadString(input);
				if (skeletonData.imagesPath.Length == 0) skeletonData.imagesPath = null;
			}

			// Bones.
			for (int i = 0, n = ReadInt(input, true); i < n; i++) {
				String name = ReadString(input);
				BoneData parent = null;
				int parentIndex = ReadInt(input, true) - 1;
				if (parentIndex != -1) parent = skeletonData.bones[parentIndex];
				BoneData boneData = new BoneData(name, parent);
				boneData.x = ReadFloat(input) * scale;
				boneData.y = ReadFloat(input) * scale;
				boneData.scaleX = ReadFloat(input);
				boneData.scaleY = ReadFloat(input);
				boneData.rotation = ReadFloat(input);
				boneData.length = ReadFloat(input) * scale;
				boneData.flipX = ReadBoolean(input);
				boneData.flipY = ReadBoolean(input);
				boneData.inheritScale = ReadBoolean(input);
				boneData.inheritRotation = ReadBoolean(input);
				if (nonessential) ReadInt(input); // Skip bone color.
				skeletonData.bones.Add(boneData);
			}

			// IK constraints.
			for (int i = 0, n = ReadInt(input, true); i < n; i++) {
				IkConstraintData ikConstraintData = new IkConstraintData(ReadString(input));
				for (int ii = 0, nn = ReadInt(input, true); ii < nn; ii++)
					ikConstraintData.bones.Add(skeletonData.bones[ReadInt(input, true)]);
				ikConstraintData.target = skeletonData.bones[ReadInt(input, true)];
				ikConstraintData.mix = ReadFloat(input);
				ikConstraintData.bendDirection = ReadSByte(input);
				skeletonData.ikConstraints.Add(ikConstraintData);
			}

			// Slots.
			for (int i = 0, n = ReadInt(input, true); i < n; i++) {
				String slotName = ReadString(input);
				BoneData boneData = skeletonData.bones[ReadInt(input, true)];
				SlotData slotData = new SlotData(slotName, boneData);
				int color = ReadInt(input);
				slotData.r = ((color & 0xff000000) >> 24) / 255f;
				slotData.g = ((color & 0x00ff0000) >> 16) / 255f;
				slotData.b = ((color & 0x0000ff00) >> 8) / 255f;
				slotData.a = ((color & 0x000000ff)) / 255f;
				slotData.attachmentName = ReadString(input);
				slotData.additiveBlending = ReadBoolean(input);
				skeletonData.slots.Add(slotData);
			}

			// Default skin.
			Skin defaultSkin = ReadSkin(input, "default", nonessential);
			if (defaultSkin != null) {
				skeletonData.defaultSkin = defaultSkin;
				skeletonData.skins.Add(defaultSkin);
			}

			// Skins.
			for (int i = 0, n = ReadInt(input, true); i < n; i++)
				skeletonData.skins.Add(ReadSkin(input, ReadString(input), nonessential));

			// Events.
			for (int i = 0, n = ReadInt(input, true); i < n; i++) {
				EventData eventData = new EventData(ReadString(input));
				eventData.Int = ReadInt(input, false);
				eventData.Float = ReadFloat(input);
				eventData.String = ReadString(input);
				skeletonData.events.Add(eventData);
			}

			// Animations.
			for (int i = 0, n = ReadInt(input, true); i < n; i++)
				ReadAnimation(ReadString(input), input, skeletonData);

			skeletonData.bones.TrimExcess();
			skeletonData.slots.TrimExcess();
			skeletonData.skins.TrimExcess();
			skeletonData.events.TrimExcess();
			skeletonData.animations.TrimExcess();
			skeletonData.ikConstraints.TrimExcess();
			return skeletonData;
		}

		public static string GetVersionString (Stream input) {
			if (input == null) throw new ArgumentNullException("input");

			try {
				// Hash.
				int byteCount = ReadVarint(input, true);
				if (byteCount > 1) input.Position += byteCount - 1;

				// Version.
				byteCount = ReadVarint(input, true);
				if (byteCount > 1) {
					byteCount--;
					var buffer = new byte[byteCount];
					ReadFully(input, buffer, 0, byteCount);
					return System.Text.Encoding.UTF8.GetString(buffer, 0, byteCount);
				}

				throw new ArgumentException("Stream does not contain a valid binary Skeleton Data.", "input");
			} catch (Exception e) {
				throw new ArgumentException("Stream does not contain a valid binary Skeleton Data.\n" + e, "input");
			}
		}

		/** @return May be null. */
		private Skin ReadSkin (Stream input, String skinName, bool nonessential) {
			int slotCount = ReadInt(input, true);
			if (slotCount == 0) return null;
			Skin skin = new Skin(skinName);
			for (int i = 0; i < slotCount; i++) {
				int slotIndex = ReadInt(input, true);
				for (int ii = 0, nn = ReadInt(input, true); ii < nn; ii++) {
					String name = ReadString(input);
					skin.AddAttachment(slotIndex, name, ReadAttachment(input, skin, name, nonessential));
				}
			}
			return skin;
		}

		private Attachment ReadAttachment (Stream input, Skin skin, String attachmentName, bool nonessential) {
			float scale = Scale;

			String name = ReadString(input);
			if (name == null) name = attachmentName;

			switch ((AttachmentType)input.ReadByte()) {
			case AttachmentType.region: {
				String path = ReadString(input);
				if (path == null) path = name;
				RegionAttachment region = attachmentLoader.NewRegionAttachment(skin, name, path);
				if (region == null) return null;
				region.Path = path;
				region.x = ReadFloat(input) * scale;
				region.y = ReadFloat(input) * scale;
				region.scaleX = ReadFloat(input);
				region.scaleY = ReadFloat(input);
				region.rotation = ReadFloat(input);
				region.width = ReadFloat(input) * scale;
				region.height = ReadFloat(input) * scale;
				int color = ReadInt(input);
				region.r = ((color & 0xff000000) >> 24) / 255f;
				region.g = ((color & 0x00ff0000) >> 16) / 255f;
				region.b = ((color & 0x0000ff00) >> 8) / 255f;
				region.a = ((color & 0x000000ff)) / 255f;
				region.UpdateOffset();
				return region;
			}
			case AttachmentType.boundingbox: {
				BoundingBoxAttachment box = attachmentLoader.NewBoundingBoxAttachment(skin, name);
				if (box == null) return null;
				box.vertices = ReadFloatArray(input, scale);
				return box;
			}
			case AttachmentType.mesh: {
				String path = ReadString(input);
				if (path == null) path = name;
				MeshAttachment mesh = attachmentLoader.NewMeshAttachment(skin, name, path);
				if (mesh == null) return null;
				mesh.Path = path;
				mesh.regionUVs = ReadFloatArray(input, 1);
				mesh.triangles = ReadShortArray(input);
				mesh.vertices = ReadFloatArray(input, scale);
				mesh.UpdateUVs();
				int color = ReadInt(input);
				mesh.r = ((color & 0xff000000) >> 24) / 255f;
				mesh.g = ((color & 0x00ff0000) >> 16) / 255f;
				mesh.b = ((color & 0x0000ff00) >> 8) / 255f;
				mesh.a = ((color & 0x000000ff)) / 255f;
				mesh.HullLength = ReadInt(input, true) * 2;
				if (nonessential) {
					mesh.Edges = ReadIntArray(input);
					mesh.Width = ReadFloat(input) * scale;
					mesh.Height = ReadFloat(input) * scale;
				}
				return mesh;
			}
			case AttachmentType.skinnedmesh: {
				String path = ReadString(input);
				if (path == null) path = name;
				SkinnedMeshAttachment mesh = attachmentLoader.NewSkinnedMeshAttachment(skin, name, path);
				if (mesh == null) return null;
				mesh.Path = path;
				float[] uvs = ReadFloatArray(input, 1);
				int[] triangles = ReadShortArray(input);

				int vertexCount = ReadInt(input, true);
				var weights = new List<float>(uvs.Length * 3 * 3);
				var bones = new List<int>(uvs.Length * 3);
				for (int i = 0; i < vertexCount; i++) {
					int boneCount = (int)ReadFloat(input);
					bones.Add(boneCount);
					for (int nn = i + boneCount * 4; i < nn; i += 4) {
						bones.Add((int)ReadFloat(input));
						weights.Add(ReadFloat(input) * scale);
						weights.Add(ReadFloat(input) * scale);
						weights.Add(ReadFloat(input));
					}
				}
				mesh.bones = bones.ToArray();
				mesh.weights = weights.ToArray();
				mesh.triangles = triangles;
				mesh.regionUVs = uvs;
				mesh.UpdateUVs();
				int color = ReadInt(input);
				mesh.r = ((color & 0xff000000) >> 24) / 255f;
				mesh.g = ((color & 0x00ff0000) >> 16) / 255f;
				mesh.b = ((color & 0x0000ff00) >> 8) / 255f;
				mesh.a = ((color & 0x000000ff)) / 255f;
				mesh.HullLength = ReadInt(input, true) * 2;
				if (nonessential) {
					mesh.Edges = ReadIntArray(input);
					mesh.Width = ReadFloat(input) * scale;
					mesh.Height = ReadFloat(input) * scale;
				}
				return mesh;
			}
			}
			return null;
		}

		private float[] ReadFloatArray (Stream input, float scale) {
			int n = ReadInt(input, true);
			float[] array = new float[n];
			if (scale == 1) {
				for (int i = 0; i < n; i++)
					array[i] = ReadFloat(input);
			} else {
				for (int i = 0; i < n; i++)
					array[i] = ReadFloat(input) * scale;
			}
			return array;
		}

		private int[] ReadShortArray (Stream input) {
			int n = ReadInt(input, true);
			int[] array = new int[n];
			for (int i = 0; i < n; i++)
				array[i] = (input.ReadByte() << 8) + input.ReadByte();
			return array;
		}

		private int[] ReadIntArray (Stream input) {
			int n = ReadInt(input, true);
			int[] array = new int[n];
			for (int i = 0; i < n; i++)
				array[i] = ReadInt(input, true);
			return array;
		}

		private void ReadAnimation (String name, Stream input, SkeletonData skeletonData) {
			var timelines = new List<Timeline>();
			float scale = Scale;
			float duration = 0;
	
			// Slot timelines.
			for (int i = 0, n = ReadInt(input, true); i < n; i++) {
				int slotIndex = ReadInt(input, true);
				for (int ii = 0, nn = ReadInt(input, true); ii < nn; ii++) {
					int timelineType = input.ReadByte();
					int frameCount = ReadInt(input, true);
					switch (timelineType) {
					case TIMELINE_COLOR: {
						ColorTimeline timeline = new ColorTimeline(frameCount);
						timeline.slotIndex = slotIndex;
						for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {
							float time = ReadFloat(input);
							int color = ReadInt(input);
							float r = ((color & 0xff000000) >> 24) / 255f;
							float g = ((color & 0x00ff0000) >> 16) / 255f;
							float b = ((color & 0x0000ff00) >> 8) / 255f;
							float a = ((color & 0x000000ff)) / 255f;
							timeline.SetFrame(frameIndex, time, r, g, b, a);
							if (frameIndex < frameCount - 1) ReadCurve(input, frameIndex, timeline);
						}
						timelines.Add(timeline);
						duration = Math.Max(duration, timeline.frames[frameCount * 5 - 5]);
						break;
					}
					case TIMELINE_ATTACHMENT: {
						AttachmentTimeline timeline = new AttachmentTimeline(frameCount);
						timeline.slotIndex = slotIndex;
						for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
							timeline.SetFrame(frameIndex, ReadFloat(input), ReadString(input));
						timelines.Add(timeline);
						duration = Math.Max(duration, timeline.frames[frameCount - 1]);
						break;
					}
					}
				}
			}

			// Bone timelines.
			for (int i = 0, n = ReadInt(input, true); i < n; i++) {
				int boneIndex = ReadInt(input, true);
				for (int ii = 0, nn = ReadInt(input, true); ii < nn; ii++) {
					int timelineType = input.ReadByte();
					int frameCount = ReadInt(input, true);
					switch (timelineType) {
					case TIMELINE_ROTATE: {
						RotateTimeline timeline = new RotateTimeline(frameCount);
						timeline.boneIndex = boneIndex;
						for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {
							timeline.SetFrame(frameIndex, ReadFloat(input), ReadFloat(input));
							if (frameIndex < frameCount - 1) ReadCurve(input, frameIndex, timeline);
						}
						timelines.Add(timeline);
						duration = Math.Max(duration, timeline.frames[frameCount * 2 - 2]);
						break;
					}
					case TIMELINE_TRANSLATE:
					case TIMELINE_SCALE: {
						TranslateTimeline timeline;
						float timelineScale = 1;
						if (timelineType == TIMELINE_SCALE)
							timeline = new ScaleTimeline(frameCount);
						else {
							timeline = new TranslateTimeline(frameCount);
							timelineScale = scale;
						}
						timeline.boneIndex = boneIndex;
						for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {
							timeline.SetFrame(frameIndex, ReadFloat(input), ReadFloat(input) * timelineScale, ReadFloat(input)
								* timelineScale);
							if (frameIndex < frameCount - 1) ReadCurve(input, frameIndex, timeline);
						}
						timelines.Add(timeline);
						duration = Math.Max(duration, timeline.frames[frameCount * 3 - 3]);
						break;
					}
					case TIMELINE_FLIPX:
					case TIMELINE_FLIPY: {
						FlipXTimeline timeline = timelineType == TIMELINE_FLIPX ? new FlipXTimeline(frameCount) : new FlipYTimeline(
							frameCount);
						timeline.boneIndex = boneIndex;
						for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
							timeline.SetFrame(frameIndex, ReadFloat(input), ReadBoolean(input));
						timelines.Add(timeline);
						duration = Math.Max(duration, timeline.frames[frameCount * 2 - 2]);
						break;
					}
					}
				}
			}

			// IK timelines.
			for (int i = 0, n = ReadInt(input, true); i < n; i++) {
				IkConstraintData ikConstraint = skeletonData.ikConstraints[ReadInt(input, true)];
				int frameCount = ReadInt(input, true);
				IkConstraintTimeline timeline = new IkConstraintTimeline(frameCount);
				timeline.ikConstraintIndex = skeletonData.ikConstraints.IndexOf(ikConstraint);
				for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {
					timeline.SetFrame(frameIndex, ReadFloat(input), ReadFloat(input), ReadSByte(input));
					if (frameIndex < frameCount - 1) ReadCurve(input, frameIndex, timeline);
				}
				timelines.Add(timeline);
				duration = Math.Max(duration, timeline.frames[frameCount * 3 - 3]);
			}

			// FFD timelines.
			for (int i = 0, n = ReadInt(input, true); i < n; i++) {
				Skin skin = skeletonData.skins[ReadInt(input, true)];
				for (int ii = 0, nn = ReadInt(input, true); ii < nn; ii++) {
					int slotIndex = ReadInt(input, true);
					for (int iii = 0, nnn = ReadInt(input, true); iii < nnn; iii++) {
						Attachment attachment = skin.GetAttachment(slotIndex, ReadString(input));
						int frameCount = ReadInt(input, true);
						FFDTimeline timeline = new FFDTimeline(frameCount);
						timeline.slotIndex = slotIndex;
						timeline.attachment = attachment;
						for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {
							float time = ReadFloat(input);

							float[] vertices;
							int vertexCount;
							if (attachment is MeshAttachment)
								vertexCount = ((MeshAttachment)attachment).vertices.Length;
							else
								vertexCount = ((SkinnedMeshAttachment)attachment).weights.Length / 3 * 2;

							int end = ReadInt(input, true);
							if (end == 0) {
								if (attachment is MeshAttachment)
									vertices = ((MeshAttachment)attachment).vertices;
								else
									vertices = new float[vertexCount];
							} else {
								vertices = new float[vertexCount];
								int start = ReadInt(input, true);
								end += start;
								if (scale == 1) {
									for (int v = start; v < end; v++)
										vertices[v] = ReadFloat(input);
								} else {
									for (int v = start; v < end; v++)
										vertices[v] = ReadFloat(input) * scale;
								}
								if (attachment is MeshAttachment) {
									float[] meshVertices = ((MeshAttachment)attachment).vertices;
									for (int v = 0, vn = vertices.Length; v < vn; v++)
										vertices[v] += meshVertices[v];
								}
							}

							timeline.SetFrame(frameIndex, time, vertices);
							if (frameIndex < frameCount - 1) ReadCurve(input, frameIndex, timeline);
						}
						timelines.Add(timeline);
						duration = Math.Max(duration, timeline.frames[frameCount - 1]);
					}
				}
			}

			// Draw order timeline.
			int drawOrderCount = ReadInt(input, true);
			if (drawOrderCount > 0) {
				DrawOrderTimeline timeline = new DrawOrderTimeline(drawOrderCount);
				int slotCount = skeletonData.slots.Count;
				for (int i = 0; i < drawOrderCount; i++) {
					int offsetCount = ReadInt(input, true);
					int[] drawOrder = new int[slotCount];
					for (int ii = slotCount - 1; ii >= 0; ii--)
						drawOrder[ii] = -1;
					int[] unchanged = new int[slotCount - offsetCount];
					int originalIndex = 0, unchangedIndex = 0;
					for (int ii = 0; ii < offsetCount; ii++) {
						int slotIndex = ReadInt(input, true);
						// Collect unchanged items.
						while (originalIndex != slotIndex)
							unchanged[unchangedIndex++] = originalIndex++;
						// Set changed items.
						drawOrder[originalIndex + ReadInt(input, true)] = originalIndex++;
					}
					// Collect remaining unchanged items.
					while (originalIndex < slotCount)
						unchanged[unchangedIndex++] = originalIndex++;
					// Fill in unchanged items.
					for (int ii = slotCount - 1; ii >= 0; ii--)
						if (drawOrder[ii] == -1) drawOrder[ii] = unchanged[--unchangedIndex];
					timeline.SetFrame(i, ReadFloat(input), drawOrder);
				}
				timelines.Add(timeline);
				duration = Math.Max(duration, timeline.frames[drawOrderCount - 1]);
			}

			// Event timeline.
			int eventCount = ReadInt(input, true);
			if (eventCount > 0) {
				EventTimeline timeline = new EventTimeline(eventCount);
				for (int i = 0; i < eventCount; i++) {
					float time = ReadFloat(input);
					EventData eventData = skeletonData.events[ReadInt(input, true)];
					Event e = new Event(eventData);
					e.Int = ReadInt(input, false);
					e.Float = ReadFloat(input);
					e.String = ReadBoolean(input) ? ReadString(input) : eventData.String;
					timeline.SetFrame(i, time, e);
				}
				timelines.Add(timeline);
				duration = Math.Max(duration, timeline.frames[eventCount - 1]);
			}

			timelines.TrimExcess();
			skeletonData.animations.Add(new Animation(name, timelines, duration));
		}

		private void ReadCurve (Stream input, int frameIndex, CurveTimeline timeline) {
			switch (input.ReadByte()) {
			case CURVE_STEPPED:
				timeline.SetStepped(frameIndex);
				break;
			case CURVE_BEZIER:
				timeline.SetCurve(frameIndex, ReadFloat(input), ReadFloat(input), ReadFloat(input), ReadFloat(input));
				break;
			}
		}

		private sbyte ReadSByte (Stream input) {
			int value = input.ReadByte();
			if (value == -1) throw new EndOfStreamException();
			return (sbyte)value;
		}

		private bool ReadBoolean (Stream input) {
			return input.ReadByte() != 0;
		}

		private float ReadFloat (Stream input) {
			buffer[3] = (byte)input.ReadByte();
			buffer[2] = (byte)input.ReadByte();
			buffer[1] = (byte)input.ReadByte();
			buffer[0] = (byte)input.ReadByte();
			return BitConverter.ToSingle(buffer, 0);
		}

		private int ReadInt (Stream input) {
			return (input.ReadByte() << 24) + (input.ReadByte() << 16) + (input.ReadByte() << 8) + input.ReadByte();
		}

		private int ReadInt (Stream input, bool optimizePositive) {
			int b = input.ReadByte();
			int result = b & 0x7F;
			if ((b & 0x80) != 0) {
				b = input.ReadByte();
				result |= (b & 0x7F) << 7;
				if ((b & 0x80) != 0) {
					b = input.ReadByte();
					result |= (b & 0x7F) << 14;
					if ((b & 0x80) != 0) {
						b = input.ReadByte();
						result |= (b & 0x7F) << 21;
						if ((b & 0x80) != 0) {
							b = input.ReadByte();
							result |= (b & 0x7F) << 28;
						}
					}
				}
			}
			return optimizePositive ? result : ((result >> 1) ^ -(result & 1));
		}

		private string ReadString (Stream input) {
			int charCount = ReadInt(input, true);
			switch (charCount) {
			case 0:
				return null;
			case 1:
				return "";
			}
			charCount--;
			char[] chars = this.chars;
			if (chars.Length < charCount) this.chars = chars = new char[charCount];
			// Try to read 7 bit ASCII chars.
			int charIndex = 0;
			int b = 0;
			while (charIndex < charCount) {
				b = input.ReadByte();
				if (b > 127) break;
				chars[charIndex++] = (char)b;
			}
			// If a char was not ASCII, finish with slow path.
			if (charIndex < charCount) ReadUtf8_slow(input, charCount, charIndex, b);
			return new String(chars, 0, charCount);
		}

		private void ReadUtf8_slow (Stream input, int charCount, int charIndex, int b) {
			char[] chars = this.chars;
			while (true) {
				switch (b >> 4) {
				case 0:
				case 1:
				case 2:
				case 3:
				case 4:
				case 5:
				case 6:
				case 7:
					chars[charIndex] = (char)b;
					break;
				case 12:
				case 13:
					chars[charIndex] = (char)((b & 0x1F) << 6 | input.ReadByte() & 0x3F);
					break;
				case 14:
					chars[charIndex] = (char)((b & 0x0F) << 12 | (input.ReadByte() & 0x3F) << 6 | input.ReadByte() & 0x3F);
					break;
				}
				if (++charIndex >= charCount) break;
				b = input.ReadByte() & 0xFF;
			}
		}

		private static int ReadVarint (Stream input, bool optimizePositive) {
			int b = input.ReadByte();
			int result = b & 0x7F;
			if ((b & 0x80) != 0) {
				b = input.ReadByte();
				result |= (b & 0x7F) << 7;
				if ((b & 0x80) != 0) {
					b = input.ReadByte();
					result |= (b & 0x7F) << 14;
					if ((b & 0x80) != 0) {
						b = input.ReadByte();
						result |= (b & 0x7F) << 21;
						if ((b & 0x80) != 0) result |= (input.ReadByte() & 0x7F) << 28;
					}
				}
			}
			return optimizePositive ? result : ((result >> 1) ^ -(result & 1));
		}


		private static void ReadFully (Stream input, byte[] buffer, int offset, int length) {
			while (length > 0) {
				int count = input.Read(buffer, offset, length);
				if (count <= 0) throw new EndOfStreamException();
				offset += count;
				length -= count;
			}
		}
#endif //OPTIMIZE_SPINE_READ

#if OPTIMIZE_SPINE
        //HuaHua
        private void WriteString(Stream output, string value, bool saveIndex = true)
        {
            if (IsOptimizedMode && saveIndex)
            {
                //save index
                var idx = CacheStrings.IndexOf(value);
                WriteInt(output, idx, true);
                return;
            }

            //save string
            if (value == null)
            {
				WriteInt(output, 0, true);
				return;
            }

            int charCount = value.Length;
            if (charCount == 0)
            {
				WriteInt(output, 1, true);
                return;
            }

            WriteInt(output, charCount + 1, true);
            int charIndex = 0;

            // Try to write 7 bit chars.
            byte[] buffer = this.buffer;
			if (buffer.Length < charCount * 3)
			{
				this.buffer = buffer = new byte[charCount * 3];
			}
            int p = 0;
            while (true)
            {
                int c = value[charIndex];
                if (c > 127) break;
                buffer[p++] = (byte)c;
                charIndex++;
                if (charIndex == charCount)
                {
                    output.Write(buffer, 0, p);
                    return;
                }
            }

            if (charIndex < charCount) WriteUtf8_slow(output, value, charCount, charIndex, ref p);
            output.Write(buffer, 0, p);
        }
        

        private static void WriteVarint(Stream output, int value, bool optimizePositive)
        {
            if (!optimizePositive) value = (value << 1) ^ (value >> 31);
            if (value >> 7 == 0)
            {
                output.WriteByte((byte)value);
                return;
            }
            if (value >> 14 == 0)
            {
                output.WriteByte((byte)((value & 0x7F) | 0x80));
                output.WriteByte((byte)(value >> 7));
                return;
            }
            if (value >> 21 == 0)
            {
                output.WriteByte((byte)((value & 0x7F) | 0x80));
                output.WriteByte((byte)(value >> 7 | 0x80));
                output.WriteByte((byte)(value >> 14));
                return;
            }
            if (value >> 28 == 0)
            {
                output.WriteByte((byte)((value & 0x7F) | 0x80));
                output.WriteByte((byte)(value >> 7 | 0x80));
                output.WriteByte((byte)(value >> 14 | 0x80));
                output.WriteByte((byte)(value >> 21));
                return;
            }

            output.WriteByte((byte)((value & 0x7F) | 0x80));
            output.WriteByte((byte)(value >> 7 | 0x80));
            output.WriteByte((byte)(value >> 14 | 0x80));
            output.WriteByte((byte)(value >> 21 | 0x80));
            output.WriteByte((byte)(value >> 28));
        }

        private void WriteUtf8_slow(Stream output, string value, int charCount, int charIndex, ref int position)
        {
            for (; charIndex < charCount; charIndex++)
            {
                int c = value[charIndex];
                if (c <= 0x007F)
                {
                    buffer[position++] = (byte)c;
                }
                else if (c > 0x07FF)
                {
                    buffer[position++] = (byte)(0xE0 | c >> 12 & 0x0F);
                    buffer[position++] = (byte)(0x80 | c >> 6 & 0x3F);
                    buffer[position++] = (byte)(0x80 | c & 0x3F);
                }
                else
                {
                    buffer[position++] = (byte)(0xC0 | c >> 6 & 0x1F);
                    buffer[position++] = (byte)(0x80 | c & 0x3F);
                }
            }
        }
        private void WriteInt(Stream output, int value, bool optimizePositive)
        {
            if (!optimizePositive) value = (value << 1) ^ (value >> 31);
            if (value >> 7 == 0)
            {                                           // @1 
                output.WriteByte((byte)value);
                return;
            }
            if (value >> 14 == 0)
            {                                          // @2
                output.WriteByte((byte)((value & 0x7F) | 0x80));
                output.WriteByte((byte)(value >> 7));
                return;
            }
            if (value >> 21 == 0)
            {
                output.WriteByte((byte)((value & 0x7F) | 0x80));
                output.WriteByte((byte)(value >> 7 | 0x80));
                output.WriteByte((byte)(value >> 14));
                return;
            }
            if (value >> 28 == 0)
            {
                output.WriteByte((byte)((value & 0x7F) | 0x80));
                output.WriteByte((byte)(value >> 7 | 0x80));
                output.WriteByte((byte)(value >> 14 | 0x80));
                output.WriteByte((byte)(value >> 21));
                return;
            }
            output.WriteByte((byte)((value & 0x7F) | 0x80));
            output.WriteByte((byte)(value >> 7 | 0x80));
            output.WriteByte((byte)(value >> 14 | 0x80));
            output.WriteByte((byte)(value >> 21 | 0x80));
            output.WriteByte((byte)(value >> 28));
        }
        private void WriteInt(Stream output, int value)
        {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)(value));
        }

        private void WriteFloat(Stream output, float f)
        {
            buffer = BitConverter.GetBytes(f);
            output.WriteByte(buffer[3]);
            output.WriteByte(buffer[2]);
            output.WriteByte(buffer[1]);
            output.WriteByte(buffer[0]);
        }

        private void WriteBoolean(Stream output, bool b)
        {
            buffer = BitConverter.GetBytes(b);
            output.WriteByte(buffer[0]);
        }


        private void WriteSByte(Stream output, sbyte sb)
        {
            output.WriteByte((byte)sb);
        }

		private void WriteColor(Stream output, float r, float g, float b, float a)
        {
			int ir = (int)(r * 255);
			int ig = (int)(g * 255);
            int ib = (int)(b * 255);
            int ia = (int)(a * 255);

            int color = 0;
            color |= (int)((ir << 24) & 0xff000000);
            color |= (int)((ig << 16) & 0x00ff0000);
            color |= (int)((ib << 8) & 0x0000ff00);
            color |= (int)((ia) & 0x000000ff);
            WriteInt(output, color);
        }

        private static void WriteFully(Stream output, byte[]buffer, int offset, int length)
        {
			output.Write(buffer, offset, length);	
        }

        private void WriteCurve(Stream output, int frameIndex, CurveTimeline timeline)
        {
            var mode = timeline.GetFrameMode(frameIndex);
            output.WriteByte(mode);
            if (mode == CURVE_BEZIER)
            {
                timeline.GetBezierCurves(frameIndex, out float cx1, out float cy1, out float cx2, out float cy2);
                WriteFloat(output, cx1);
                WriteFloat(output, cy1);
                WriteFloat(output, cx2);
                WriteFloat(output, cy2);
            }
        }

        private bool IsSameCurve(CurveTimeline timeline, int lftFrameIndex, int rtFrameIndex)
        {
            var lftMode = timeline.GetFrameMode(lftFrameIndex);
            var rtMode = timeline.GetFrameMode(rtFrameIndex);
            if (lftMode == CURVE_BEZIER && rtMode == CURVE_BEZIER)
            {
                timeline.GetBezierCurves(lftFrameIndex, out float cx1, out float cy1, out float cx2, out float cy2);
                timeline.GetBezierCurves(rtFrameIndex, out float cx1_1, out float cy1_1, out float cx2_1, out float cy2_1);
                return cx1 == cx1_1 && cy1 == cy1_1 && cx2 == cx2_1 && cy2 == cy2_1;
            }

            return lftMode == rtMode;
        }

        private void WriteFloatArray(Stream output, float scale, float[] arr)
        {
			int n = arr.Length;
			WriteInt(output, n, true);
			if (scale == 1)
            {
				for(int i = 0; i < n; ++i)
                {
					WriteFloat(output, arr[i]);
                }
            }
			else
            {
                for (int i = 0; i < n; ++i)
                {
                    WriteFloat(output, arr[i] / scale);
                }
            }
        }

        private void WriteShortArray(Stream output, int[] arr)
        {
			int n = arr.Length;
			WriteInt(output, n, true);

            for (int i = 0; i < n; ++i)
            {
                output.WriteByte((byte)(arr[i] << 8));
                output.WriteByte((byte)(arr[i]));
            }
        }

        private void WriteIntArray(Stream output, int[] arr)
        {
            int n = arr.Length;
            WriteInt(output, n, true);
            for (int i = 0; i < n; ++i)
            {
				WriteInt(output, arr[i], true);
            }
        }

        private static List<Timeline> GetOrAdd(IDictionary<int, List<Timeline>> dictionary, int key)
        {
			if (dictionary.TryGetValue(key, out List<Timeline> value))
			{
				return value;
			}

            value = new List<Timeline>();
            dictionary.Add(key, value);
			return value;
        }

        private void WriteAnimation(Stream output, SkeletonData skeletonData, Animation animation)
        {
			List<Timeline> timelines = animation.Timelines;

			float scale = Scale;

            Dictionary<int, List<Timeline>> slotTimelines = new Dictionary<int, List<Timeline>>();
			Dictionary<int, List<Timeline>> boneTimelines = new Dictionary<int, List<Timeline>>();
			List<IkConstraintTimeline> ikTimelines = new List<IkConstraintTimeline>();
            Dictionary<int, Dictionary<int, List<FFDTimeline>>> ffdTimelines = new Dictionary<int, Dictionary<int, List<FFDTimeline>>>();
            EventTimeline eventTimeline = null;
			DrawOrderTimeline drawTimeline = null;


			foreach (var tl in timelines)
			{
				if (tl is ColorTimeline)
				{
					GetOrAdd(slotTimelines, (tl as ColorTimeline).slotIndex).Add(tl);
				}
				else if (tl is AttachmentTimeline)
				{
					GetOrAdd(slotTimelines, (tl as AttachmentTimeline).slotIndex).Add(tl);
				}
				else if (tl is RotateTimeline)
				{
					GetOrAdd(boneTimelines, (tl as RotateTimeline).boneIndex).Add(tl);
				}
				else if (tl is TranslateTimeline)
				{
					GetOrAdd(boneTimelines, (tl as TranslateTimeline).boneIndex).Add(tl);
				}
				else if (tl is FlipXTimeline)
				{
					GetOrAdd(boneTimelines, (tl as FlipXTimeline).boneIndex).Add(tl);
				}
				else if (tl is IkConstraintTimeline)
				{
					ikTimelines.Add(tl as IkConstraintTimeline);
				}
				else if (tl is FFDTimeline)
				{
					var timeline = tl as FFDTimeline;
					int skinIdx = -1;

					for (int i = 0; i < skeletonData.skins.Count; ++i)
					{
						var skin = skeletonData.skins[i];
						foreach (var each in skin.Attachments)
						{
							if (timeline.attachment == each.Value)
							{
								skinIdx = i;
								break;
							}
						}
						if (skinIdx != -1)
						{
							break;
						}
					}

					if (!ffdTimelines.TryGetValue(skinIdx, out Dictionary<int, List<FFDTimeline>> skinList))
                    {
						skinList = new Dictionary<int, List<FFDTimeline>>();
						ffdTimelines.Add(skinIdx, skinList);
					}

					if(!skinList.TryGetValue(timeline.slotIndex, out List<FFDTimeline> tls))
                    {
						tls = new List<FFDTimeline>();
						skinList.Add(timeline.slotIndex, tls);
					}

					tls.Add(timeline);
				}
				else if (tl is EventTimeline)
                {
					eventTimeline = tl as EventTimeline;
				}
				else if (tl is DrawOrderTimeline)
                {
					drawTimeline = tl as DrawOrderTimeline;
				}
            }

			// Slot timelines.
			WriteInt(output, slotTimelines.Count, true);
            foreach (var each in slotTimelines)
            {
				int slotIndex = each.Key;
				var tls = each.Value;

				WriteInt(output, slotIndex, true);
				int nn = tls.Count;
				WriteInt(output, nn, true);

				for(int ii = 0; ii < nn; ++ii)
                {
					var tl = tls[ii];
					if (tl is ColorTimeline)
                    {
						var timeline = tl as ColorTimeline;
						output.WriteByte(TIMELINE_COLOR); //timelineType

						int frameCount = timeline.FrameCount;
                        var frames = timeline.Frames;

                        if (IsOptimizedMode && frameCount > 3)
                        {
                            List<int> saveFrames = new List<int>() { 0 };

                            float preR = frames[1];
                            float preG = frames[2];
                            float preB = frames[3];
                            float preA = frames[4];

                            for (int frameIndex = 1; frameIndex < frameCount - 2; frameIndex++)
                            {
                                var curR = frames[frameIndex * 5 + 1];
                                var curG = frames[frameIndex * 5 + 2];
                                var curB = frames[frameIndex * 5 + 3];
                                var curA = frames[frameIndex * 5 + 4];

                                var nxtR = frames[(frameIndex + 1) * 5 + 1];
                                var nxtG = frames[(frameIndex + 1) * 5 + 2];
                                var nxtB = frames[(frameIndex + 1) * 5 + 3];
                                var nxtA = frames[(frameIndex + 1) * 5 + 4];

                                if (preR != curR || preG != curG || preB != curB || preA != curA
                                    || curR != nxtR || curG != nxtG || curB != nxtB || curA != nxtA
                                    || !IsSameCurve(timeline, frameIndex - 1, frameIndex)
                                    || !IsSameCurve(timeline, frameIndex, frameIndex + 1)
                                    )
                                {
                                    saveFrames.Add(frameIndex);
                                    preR = curR;
                                    preG = curG;
                                    preB = curB;
                                    preA = curA;
                                }
                            }
                            saveFrames.Add(frameCount - 2);
                            saveFrames.Add(frameCount - 1);
                            WriteInt(output, saveFrames.Count, true); //frameCount

                            foreach(var frameIndex in saveFrames)
                            {
                                float time = frames[frameIndex * 5];
                                WriteFloat(output, time);   //time  

                                float r = frames[frameIndex * 5 + 1];
                                float g = frames[frameIndex * 5 + 2];
                                float b = frames[frameIndex * 5 + 3];
                                float a = frames[frameIndex * 5 + 4];

                                WriteColor(output, r, g, b, a);

                                if (frameIndex < frameCount - 1)
                                {
                                    WriteCurve(output, frameIndex, timeline);
                                }
                            }
                        }
                        else
                        {
                            WriteInt(output, frameCount, true); //frameCount

                            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                            {
                                float time = frames[frameIndex * 5];
                                WriteFloat(output, time);   //time  

                                float r = frames[frameIndex * 5 + 1];
                                float g = frames[frameIndex * 5 + 2];
                                float b = frames[frameIndex * 5 + 3];
                                float a = frames[frameIndex * 5 + 4];

                                WriteColor(output, r, g, b, a);

                                if (frameIndex < frameCount - 1)
                                {
                                    WriteCurve(output, frameIndex, timeline);
                                }
                            }
                        }
                    }
					else if (tl is AttachmentTimeline)
                    {
                        var timeline = tl as AttachmentTimeline;
                        output.WriteByte(TIMELINE_ATTACHMENT); //timelineType

						var frames = timeline.Frames;
						int frameCount = timeline.FrameCount;
                        var vattachmentNames = timeline.AttachmentNames;

                        if (IsOptimizedMode && frameCount > 1)
                        {
                            List<int> saveFrames = new List<int>() { 0 };
                            var preName = vattachmentNames[0];
                            for (int frameIndex = 1; frameIndex < frameCount; frameIndex++)
                            {
                                var curName = vattachmentNames[frameIndex];
                                if (preName != curName)
                                {
                                    preName = curName;
                                    saveFrames.Add(frameIndex);
                                }
                            }

                            WriteInt(output, saveFrames.Count, true); //frameCount

                            foreach (var frameIndex in saveFrames)
                            {
                                WriteFloat(output, frames[frameIndex]);
                                WriteString(output, vattachmentNames[frameIndex]);
                            }
                        }
                        else
                        {
                            WriteInt(output, frameCount, true); //frameCount

                            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                            {
                                WriteFloat(output, frames[frameIndex]);
                                WriteString(output, vattachmentNames[frameIndex]);
                            }
                        }
                    }
                }
            }

            // Bone timelines.
            WriteInt(output, boneTimelines.Count, true);
			foreach (var each in boneTimelines)
			{
                int boneIndex = each.Key;
                var tls = each.Value;

                WriteInt(output, boneIndex, true);
                int nn = tls.Count;
                WriteInt(output, nn, true);

				for (int ii = 0; ii < nn; ++ii)
				{
					var tl = tls[ii];
					if (tl is RotateTimeline)
					{
						var timeline = tl as RotateTimeline;
						output.WriteByte(TIMELINE_ROTATE); //timelineType

                        int frameCount = timeline.FrameCount;
                        var frames = timeline.Frames;

                        if (IsOptimizedMode && frameCount > 3)
                        {
                            List<int> saveFrames = new List<int>() { 0 };

                            float preValue = frames[1];
                            for (int frameIndex = 1; frameIndex < frameCount - 2; frameIndex++)
                            {
                                var curValue = frames[frameIndex * 2 + 1];
                                var lastValue = frames[(frameIndex + 1) * 2 + 1];
                                if (preValue != curValue 
                                    || curValue != lastValue 
                                    || !IsSameCurve(timeline, frameIndex - 1, frameIndex)
                                    || !IsSameCurve(timeline, frameIndex, frameIndex + 1)
                                    )
                                {
                                    saveFrames.Add(frameIndex);
                                    preValue = curValue;
                                }
                            }
                            saveFrames.Add(frameCount - 2);
                            saveFrames.Add(frameCount - 1);

                            WriteInt(output, saveFrames.Count, true); //frameCount
                            foreach(var frameIndex in saveFrames)
                            {
                                WriteFloat(output, frames[frameIndex * 2]);
                                WriteFloat(output, frames[frameIndex * 2 + 1]);
                                if (frameIndex < frameCount - 1)
                                {
                                    WriteCurve(output, frameIndex, timeline);
                                }
                            }
                        }
                        else
                        {
                            WriteInt(output, frameCount, true); //frameCount

                            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                            {
                                WriteFloat(output, frames[frameIndex * 2]);
                                WriteFloat(output, frames[frameIndex * 2 + 1]);
                                if (frameIndex < frameCount - 1)
                                {
                                    WriteCurve(output, frameIndex, timeline);
                                }
                            }
                        }
                    }
					else if (tl is TranslateTimeline)
                    {
                        var timeline = tl as TranslateTimeline;
						float timelineScale = 1;
						if (tl is ScaleTimeline)
                        {
							output.WriteByte(TIMELINE_SCALE);
                        }
                        else
                        {
							output.WriteByte(TIMELINE_TRANSLATE);
							timelineScale = scale;
						}

                        int frameCount = timeline.FrameCount;

                        if (IsOptimizedMode && frameCount > 3)
                        {
                            var frames = timeline.Frames;

                            List<int> saveFrames = new List<int>() { 0 };

                            float preX = frames[1];
                            float preY = frames[2];
                            for (int frameIndex = 1; frameIndex < frameCount - 2; frameIndex++)
                            {
                                var curX = frames[frameIndex * 3 + 1];
                                var curY = frames[frameIndex * 3 + 2];
                                var nxtX = frames[(frameIndex + 1) * 3 + 1];
                                var nxtY = frames[(frameIndex + 1) * 3 + 2];

                                if (preX != curX || preY != curY || !IsSameCurve(timeline, frameIndex - 1, frameIndex)
                                    || nxtX != curX || nxtY != curY || !IsSameCurve(timeline, frameIndex, frameIndex + 1))
                                {
                                    saveFrames.Add(frameIndex);
                                    preX = curX;
                                    preY = curY;
                                }
                            }
                            saveFrames.Add(frameCount - 2);
                            saveFrames.Add(frameCount - 1);

                            WriteInt(output, saveFrames.Count, true); //frameCount
                            foreach (var frameIndex in saveFrames)
                            {
                                WriteFloat(output, frames[frameIndex * 3]);
                                WriteFloat(output, frames[frameIndex * 3 + 1] / timelineScale);
                                WriteFloat(output, frames[frameIndex * 3 + 2] / timelineScale);
                                if (frameIndex < frameCount - 1)
                                {
                                    WriteCurve(output, frameIndex, timeline);
                                }
                            }
                        }
                        else
                        {
                            WriteInt(output, frameCount, true); //frameCount

                            var frames = timeline.Frames;
                            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                            {
                                WriteFloat(output, frames[frameIndex * 3]);
                                WriteFloat(output, frames[frameIndex * 3 + 1] / timelineScale);
                                WriteFloat(output, frames[frameIndex * 3 + 2] / timelineScale);
                                if (frameIndex < frameCount - 1)
                                {
                                    WriteCurve(output, frameIndex, timeline);
                                }
                            }
                        }
                    }
					else if (tl is FlipXTimeline)
                    {
                        var timeline = tl as FlipXTimeline;
                        if (tl is FlipYTimeline)
                        {
                            output.WriteByte(TIMELINE_FLIPY);
                        }
                        else
                        {
                            output.WriteByte(TIMELINE_FLIPX);
                        }

                        int frameCount = timeline.FrameCount;
                        var frames = timeline.Frames;

                        if (IsOptimizedMode && frameCount > 1)
                        {
                            List<int> saveFrames = new List<int>() { 0 };

                            bool preValue = frames[1] == 1;
                            for (int frameIndex = 1; frameIndex < frameCount; frameIndex++)
                            {
                                var curValue = frames[frameIndex * 2 + 1] == 1;
                                if (preValue != curValue)
                                {
                                    saveFrames.Add(frameIndex);
                                    preValue = curValue;
                                }
                            }

                            WriteInt(output, saveFrames.Count, true); //frameCount
                            foreach (var frameIndex in saveFrames)
                            {
                                WriteFloat(output, frames[frameIndex * 2]);
                                WriteBoolean(output, frames[frameIndex * 2 + 1] == 1);
                            }
                        }
                        else
                        {
                            WriteInt(output, frameCount, true); //frameCount

                            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                            {
                                WriteFloat(output, frames[frameIndex * 2]);
                                WriteBoolean(output, frames[frameIndex * 2 + 1] == 1);
                            }
                        }
                    }
                }
            }


            // IK timelines.
			WriteInt(output, ikTimelines.Count, true);
			for(int i = 0; i < ikTimelines.Count; ++i)
            {
				IkConstraintTimeline timeline = ikTimelines[i];
                WriteInt(output, timeline.ikConstraintIndex, true);

                var frames = timeline.Frames;
                int frameCount = timeline.FrameCount;
                if (IsOptimizedMode && frameCount > 3)
                {
                    var preMix = frames[1];
                    var preDir = (sbyte)frames[2];

                    List<int> saveFrames = new List<int>() { 0 };
                    for (int frameIndex = 1; frameIndex < frameCount - 2; frameIndex++)
                    {
                        var curMix = frames[frameIndex * 3 + 1];
                        var curDir = (sbyte)frames[frameIndex * 3 + 2];
                        var nxtMix = frames[(frameIndex + 1) * 3 + 1];
                        var nxtDir = (sbyte)frames[(frameIndex + 1) * 3 + 2];
                        if (preMix != curMix || preDir != curDir || !IsSameCurve(timeline, frameIndex - 1, frameIndex)
                            || nxtMix != curMix || nxtDir != curDir || !IsSameCurve(timeline, frameIndex, frameIndex + 1))
                        {
                            saveFrames.Add(frameIndex);
                            preMix = curMix;
                            preDir = curDir;
                        }
                    }
                    saveFrames.Add(frameCount - 2);
                    saveFrames.Add(frameCount - 1);
                    WriteInt(output, saveFrames.Count, true); //frameCount
                    foreach (var frameIndex in saveFrames)
                    {
                        WriteFloat(output, frames[frameIndex * 3]);
                        WriteFloat(output, frames[frameIndex * 3 + 1]);
                        WriteSByte(output, (sbyte)frames[frameIndex * 3 + 2]);

                        if (frameIndex < frameCount - 1)
                        {
                            WriteCurve(output, frameIndex, timeline);
                        }
                    }
                }
                else
                {
                    WriteInt(output, frameCount, true);

                    for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                    {
                        WriteFloat(output, frames[frameIndex * 3]);
                        WriteFloat(output, frames[frameIndex * 3 + 1]);
                        WriteSByte(output, (sbyte)frames[frameIndex * 3 + 2]);

                        if (frameIndex < frameCount - 1)
                        {
                            WriteCurve(output, frameIndex, timeline);
                        }
                    }
                }
            }

			// FFD timelines.
			WriteInt(output, ffdTimelines.Count, true);
            foreach(var each1 in ffdTimelines)
            {
				WriteInt(output, each1.Key, true);
				WriteInt(output, each1.Value.Count, true);
				foreach(var each2 in each1.Value)
                {
					int slotIndex = each2.Key;
					WriteInt(output, slotIndex, true);
					WriteInt(output, each2.Value.Count, true);
					for (int iii = 0; iii < each2.Value.Count; ++iii)
                    {
						var timeline = each2.Value[iii];
						string attachmentName = null;
						for (int si = 0; si < skeletonData.skins.Count; ++si)
                        {
                            var skin = skeletonData.skins[si];
                            foreach (var each in skin.Attachments)
                            {
                                if (timeline.attachment == each.Value)
                                {
									attachmentName = each.Key.Value;
									break;
                                }
                            }
                            if (!string.IsNullOrEmpty(attachmentName))
                            {
                                break;
                            }
                        }
						WriteString(output, attachmentName);

						var frames = timeline.Frames;
						var vertices = timeline.Vertices;
						WriteInt(output, timeline.FrameCount, true);
						for (int frameIndex = 0; frameIndex < timeline.FrameCount; frameIndex++)
						{
							WriteFloat(output, frames[frameIndex]);
							WriteInt(output, timeline.End[frameIndex], true);
							if (timeline.End[frameIndex] > 0)
                            {
								WriteInt(output, timeline.Start[frameIndex], true);
								for(int vi = 0; vi < timeline.OffsetVertices[frameIndex].Length; ++vi)
                                {
									WriteFloat(output, timeline.OffsetVertices[frameIndex][vi]);
                                }
                            }

                            if (frameIndex < timeline.FrameCount - 1)
                            {
                                WriteCurve(output, frameIndex, timeline);
                            }
                        }
                    }
				}
            }

			// Draw order timeline.
			var drawOrders = drawTimeline?.DrawOrders;
			WriteInt(output, drawOrders == null ? 0 : drawOrders.Length, true);
			if (drawOrders != null)
			{
				var frames = drawTimeline.Frames;
				for (int i = 0; i < drawOrders.Length; i++)
				{
					var offset = drawTimeline.OffsetVertices[i];

					if (offset == null)
                    {
						WriteInt(output, 0, true);
					}
                    else
                    {
						WriteInt(output, offset.Length, true);
                        for (int ii = 0; ii < offset.Length; ++ii)
                        {
							var (slotIndex, off) = offset[ii];

							WriteInt(output, slotIndex, true);
                            WriteInt(output, off, true);
                        }
                    }

					WriteFloat(output, frames[i]);
				}
            }

            // Event timeline.
            WriteInt(output, eventTimeline == null ? 0 : eventTimeline.FrameCount, true);
			if (eventTimeline != null)
            {
				var frames = eventTimeline.Frames;
				var events = eventTimeline.Events;
				for (int i = 0; i < eventTimeline.FrameCount; i++)
				{
					WriteFloat(output, frames[i]);
					Event e = events[i];
					var eventIdx = skeletonData.events.IndexOf(e.Data);
					WriteInt(output, eventIdx, true);

					WriteInt(output, e.Int, true);
					WriteFloat(output, e.Float);
					if (e.String == e.Data.String)
                    {
						WriteBoolean(output, false);
                    }
					else
                    {
						WriteBoolean(output, true);
						WriteString(output, e.String);
                    }
				}
            }
        }

        public void WriteSkeletonData(Stream output, SkeletonData skeletonData)
        {
			float scale = Scale;

            IsOptimizedMode = CacheStrings != null && CacheStrings.Count > 2;

            // Skeleton.
            WriteString(output, skeletonData.hash, false);
			WriteString(output, IsOptimizedMode ? "HuaHua1.0" : skeletonData.version, false);
			WriteFloat(output, skeletonData.width);
			WriteFloat(output, skeletonData.height);
			WriteBoolean(output, false);

            // Write Cache Strings
            if (IsOptimizedMode)
            {
                WriteInt(output, CacheStrings.Count, true);
                foreach (var str in CacheStrings)
                {
                    WriteString(output, str, false);
                }
            }

            // Bones.
            WriteInt(output, skeletonData.bones.Count, true);
			for (int i = 0; i < skeletonData.bones.Count; i++)
            {
				BoneData boneData = skeletonData.bones[i];
				WriteString(output, boneData.Name);
				WriteInt(output, skeletonData.Bones.IndexOf(boneData.Parent) + 1, true);
				WriteFloat(output, boneData.x / scale);
				WriteFloat(output, boneData.y / scale);
				WriteFloat(output, boneData.scaleX);
				WriteFloat(output, boneData.scaleY);
				WriteFloat(output, boneData.rotation);
				WriteFloat(output, boneData.length / scale);
				WriteBoolean(output, boneData.flipX);
				WriteBoolean(output, boneData.flipY);
				WriteBoolean(output, boneData.inheritScale);
				WriteBoolean(output, boneData.inheritRotation);
			}

			// IK constraints.
			WriteInt(output, skeletonData.ikConstraints.Count, true);
			for (int i = 0; i < skeletonData.ikConstraints.Count; i++)
            {
				IkConstraintData ikConstraintData = skeletonData.ikConstraints[i];
				WriteString(output, ikConstraintData.Name);
				WriteInt(output, ikConstraintData.bones.Count, true);
				for(int ii = 0; ii < ikConstraintData.bones.Count; ++ ii)
                {
					WriteInt(output, skeletonData.Bones.IndexOf(ikConstraintData.bones[ii]), true);
                }
				WriteInt(output, skeletonData.Bones.IndexOf(ikConstraintData.target), true);
				WriteFloat(output, ikConstraintData.mix);
				WriteSByte(output, (sbyte)ikConstraintData.bendDirection);
			}

			// Slots.
			WriteInt(output, skeletonData.slots.Count, true);
			for (int i = 0; i < skeletonData.slots.Count; i++)
            {
				SlotData slotData = skeletonData.slots[i];
				WriteString(output, slotData.Name);
				WriteInt(output, skeletonData.Bones.IndexOf(slotData.BoneData), true);
				WriteColor(output, slotData.r, slotData.g, slotData.b, slotData.a);
				WriteString(output, slotData.attachmentName);
				WriteBoolean(output, slotData.additiveBlending);
			}

            // Default skin.
            WriteSkin(output, skeletonData.defaultSkin);

            // Skins.
            WriteInt(output, skeletonData.skins.Count - 1, true);
            for (int i = 1; i < skeletonData.skins.Count; ++i)
            {
				WriteString(output, skeletonData.skins[i].Name);
				WriteSkin(output, skeletonData.skins[i]);
			}

			// Events.
			WriteInt(output, skeletonData.events.Count, true);
			for (int i = 0; i < skeletonData.events.Count; i++)
            {
				EventData eventData = skeletonData.events[i];
				WriteString(output, eventData.Name);
				WriteInt(output, eventData.Int, false);
				WriteFloat(output, eventData.Float);
				WriteString(output, eventData.String);
			}

			// Animations.
			WriteInt(output, skeletonData.animations.Count, true);
			for (int i = 0; i < skeletonData.animations.Count; i++)
            {
				var animation = skeletonData.animations[i];
				WriteString(output, animation.Name);
				WriteAnimation(output, skeletonData, animation);
			}
        }

        private void WriteSkin(Stream output, Skin skin)
        {
			var attachments = skin.Attachments;
			if (attachments.Count == 0)
            {
				WriteInt(output, 0, true);
				return;
            }

			var slots = new Dictionary<int, List<KeyValuePair<String, Attachment>>>();
			foreach (var each in attachments)
            {
				if (!slots.TryGetValue(each.Key.Key, out List<KeyValuePair<String, Attachment>> ret))
                {
					ret = new List<KeyValuePair<String, Attachment>>();
					slots.Add(each.Key.Key, ret);
				}

				ret.Add( new KeyValuePair<string, Attachment>(each.Key.Value, each.Value) );
            }


			WriteInt(output, slots.Count, true);
			foreach(var each in slots)
            {
				WriteInt(output, each.Key, true);
				WriteInt(output, each.Value.Count, true);
				foreach(var each2 in each.Value)
                {
                    WriteString(output, each2.Key);
                    WriteAttachment(output, skin, each2.Key, each2.Value);
                }
            }
        }

		private void WriteAttachment(Stream output, Skin skin, String attachmentName, Attachment attachment)
		{
			float scale = Scale;

			WriteString(output, attachment.Name);

			if (attachment is RegionAttachment)
			{
				output.WriteByte((byte)AttachmentType.region);
				RegionAttachment region = attachment as RegionAttachment;
				WriteString(output, region.Path);
				WriteFloat(output, region.x / scale);
				WriteFloat(output, region.y / scale);
				WriteFloat(output, region.scaleX);
				WriteFloat(output, region.scaleY);
				WriteFloat(output, region.rotation);
				WriteFloat(output, region.width / scale);
				WriteFloat(output, region.height / scale);
				WriteColor(output, region.r, region.g, region.b, region.a);
			}
			else if (attachment is BoundingBoxAttachment)
			{
				output.WriteByte((byte)AttachmentType.boundingbox);
				BoundingBoxAttachment box = attachment as BoundingBoxAttachment;
				WriteFloatArray(output, scale, box.vertices);
			}
			else if (attachment is MeshAttachment)
			{
				output.WriteByte((byte)AttachmentType.mesh);
				MeshAttachment mesh = attachment as MeshAttachment;
				WriteString(output, mesh.Path);
				WriteFloatArray(output, 1, mesh.regionUVs);
				WriteShortArray(output, mesh.triangles);
				WriteFloatArray(output, scale, mesh.vertices);
				WriteColor(output, mesh.r, mesh.g, mesh.b, mesh.a);
				WriteInt(output, mesh.HullLength / 2, true);
			}
			else if (attachment is SkinnedMeshAttachment)
			{
				output.WriteByte((byte)AttachmentType.skinnedmesh);
				SkinnedMeshAttachment mesh = attachment as SkinnedMeshAttachment;
				WriteString(output, mesh.Path);
				WriteFloatArray(output, 1, mesh.regionUVs);
				WriteShortArray(output, mesh.triangles);
				WriteInt(output, mesh.bones.Length + mesh.weights.Length, true);
				int iWeight = 0;
				for (int i = 0; i < mesh.bones.Length;)
				{
					int boneCount = mesh.bones[i++];
					WriteFloat(output, boneCount);
					for (int nn = 0; nn < boneCount * 3; nn += 3)
					{
						WriteFloat(output, mesh.bones[i++]);
						WriteFloat(output, mesh.weights[iWeight + nn + 0] / scale);
						WriteFloat(output, mesh.weights[iWeight + nn + 1] / scale);
						WriteFloat(output, mesh.weights[iWeight + nn + 2]);
					}
					iWeight += boneCount * 3;
				}
				WriteColor(output, mesh.r, mesh.g, mesh.b, mesh.a);
				WriteInt(output, mesh.HullLength / 2, true);

			}
		}
#endif //OPTIMIZE_SPINE
	}
}
