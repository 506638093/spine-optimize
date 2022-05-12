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

using System;
using System.Collections.Generic;

namespace Spine {
	public class AtlasAttachmentLoader : AttachmentLoader {
		private Atlas[] atlasArray;

		// HuaHua. cache regions, quickly find
		private Dictionary<string, AtlasRegion> regins = new Dictionary<string, AtlasRegion>();

        public AtlasAttachmentLoader(params Atlas[] atlasArray)
		{
			if (atlasArray == null) throw new ArgumentNullException("atlas array cannot be null.");
			this.atlasArray = atlasArray;

			// HuaHua.
			for (int i = 0; i < atlasArray.Length; i++)
			{
				var regions = atlasArray[i].regions;

				for (int ir = 0, n = regions.Count; ir < n; ir++)
                {
					regins.Add(regions[ir].name, regions[ir]);
				}
			}
		}

		public RegionAttachment NewRegionAttachment (Skin skin, String name, String path) {
			AtlasRegion region = FindRegion(path);
			if (region == null) throw new Exception("Region not found in atlas: " + path + " (region attachment: " + name + ")");
			RegionAttachment attachment = new RegionAttachment(name);
			attachment.RendererObject = region;
			attachment.SetUVs(region.u, region.v, region.u2, region.v2, region.rotate);
			attachment.regionOffsetX = region.offsetX;
			attachment.regionOffsetY = region.offsetY;
			attachment.regionWidth = region.width;
			attachment.regionHeight = region.height;
			attachment.regionOriginalWidth = region.originalWidth;
			attachment.regionOriginalHeight = region.originalHeight;
			return attachment;
		}

		public MeshAttachment NewMeshAttachment (Skin skin, String name, String path) {
			AtlasRegion region = FindRegion(path);
			if (region == null) throw new Exception("Region not found in atlas: " + path + " (mesh attachment: " + name + ")");
			MeshAttachment attachment = new MeshAttachment(name);
			attachment.RendererObject = region;
			attachment.RegionU = region.u;
			attachment.RegionV = region.v;
			attachment.RegionU2 = region.u2;
			attachment.RegionV2 = region.v2;
			attachment.RegionRotate = region.rotate;
			attachment.regionOffsetX = region.offsetX;
			attachment.regionOffsetY = region.offsetY;
			attachment.regionWidth = region.width;
			attachment.regionHeight = region.height;
			attachment.regionOriginalWidth = region.originalWidth;
			attachment.regionOriginalHeight = region.originalHeight;
			return attachment;
		}

		public SkinnedMeshAttachment NewSkinnedMeshAttachment (Skin skin, String name, String path) {
			AtlasRegion region = FindRegion(path);
			if (region == null) throw new Exception("Region not found in atlas: " + path + " (skinned mesh attachment: " + name + ")");
			SkinnedMeshAttachment attachment = new SkinnedMeshAttachment(name);
			attachment.RendererObject = region;
			attachment.RegionU = region.u;
			attachment.RegionV = region.v;
			attachment.RegionU2 = region.u2;
			attachment.RegionV2 = region.v2;
			attachment.RegionRotate = region.rotate;
			attachment.regionOffsetX = region.offsetX;
			attachment.regionOffsetY = region.offsetY;
			attachment.regionWidth = region.width;
			attachment.regionHeight = region.height;
			attachment.regionOriginalWidth = region.originalWidth;
			attachment.regionOriginalHeight = region.originalHeight;
			return attachment;
		}

		public BoundingBoxAttachment NewBoundingBoxAttachment (Skin skin, String name) {
			return new BoundingBoxAttachment(name);
		}

		public AtlasRegion FindRegion(string name) {
			regins.TryGetValue(name, out AtlasRegion region);
			return region;
		}
	}
}
