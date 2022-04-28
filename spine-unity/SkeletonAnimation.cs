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
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Spine;
using AnimationState = Spine.AnimationState;
using Event = Spine.Event;

[ExecuteInEditMode]
[AddComponentMenu("Spine/SkeletonAnimation")]
public class SkeletonAnimation : SkeletonRenderer, ISkeletonAnimation
{
    public float timeScale = 1;
    public bool loop;
    public Spine.AnimationState state;


    public event UpdateBonesDelegate UpdateLocal
    {
        add { _UpdateLocal += value; }
        remove { _UpdateLocal -= value; }
    }

    public event UpdateBonesDelegate UpdateWorld
    {
        add { _UpdateWorld += value; }
        remove { _UpdateWorld -= value; }
    }

    public event UpdateBonesDelegate UpdateComplete
    {
        add { _UpdateComplete += value; }
        remove { _UpdateComplete -= value; }
    }

    protected event UpdateBonesDelegate _UpdateLocal;
    protected event UpdateBonesDelegate _UpdateWorld;
    protected event UpdateBonesDelegate _UpdateComplete;


    //给lua侧用的
    public Action<AnimationState, int, int> OnComplete;
    public Action<AnimationState, int> OnStart;
    public Action<AnimationState, int> OnEnd;
    public Action<AnimationState, int, Event> OnEvent;


    [SerializeField] private String
        _animationName;

    public String AnimationName
    {
        get
        {
            TrackEntry entry = state.GetCurrent(0);
            return entry == null ? null : entry.Animation.Name;
        }
        set
        {
            _animationName = value;
            if (value == null || value.Length == 0)
                state.ClearTrack(0);
            else
                state.SetAnimation(0, value, loop);
        }
    }

    public void PlayAni(string animation, bool loop)
    {
        _animationName = animation;
        if (animation == null || animation.Length == 0)
            state.ClearTrack(0);
        else
            state.SetAnimation(0, animation, loop);
    }

    public override void Reset()
    {
        base.Reset();
        if (!valid)
            return;

        state = new Spine.AnimationState(skeletonDataAsset.GetAnimationStateData());
        state.Complete += OnAniCompeted;
        state.Start += OnAniStart;
        state.End += OnAniEnd;
        state.Event += OnAniEvent;
        if (_animationName != null && _animationName.Length > 0)
        {
            state.SetAnimation(0, _animationName, loop);
            Update(0);
        }
    }

    private void OnAniCompeted(AnimationState aniState, int index, int loopNum)
    {
        OnComplete?.Invoke(aniState, index, loopNum);
    }

    private void OnAniStart(AnimationState aniState, int index)
    {
        OnStart?.Invoke(aniState, index);
    }

    private void OnAniEnd(AnimationState aniState, int index)
    {
        OnEnd?.Invoke(aniState, index);
    }

    private void OnAniEvent(AnimationState aniState, int index, Event e)
    {
        //Debug.LogWarning($"OnAniEvent {AnimationName}----------------{e.ToString()}");
        OnEvent?.Invoke(aniState, index, e);
    }


    readonly List<Vector2> _verts = new List<Vector2>();

    public Rect GetWorldPosRectBySlot(string slotName)
    {
        Bone bone = skeleton.FindSlot(slotName)?.Bone;
//		_bone.localToWorld(_bone.x, _bone.y, out float wx, out float wy);
        Attachment attachment = skeleton.GetAttachment(slotName, slotName);

        if (attachment == null || bone == null)
        {
            return Rect.zero;
        }

        //	
        if (attachment is BoundingBoxAttachment boundingBox)
        {
            var floats = boundingBox.Vertices;
            var floatCount = floats.Length;
            //Debug.Log($"wx{_bone.worldX} ,, wy {_bone.worldY} ,  x {_bone.x}   ,y {_bone.y}");

            _verts.Clear();
            for (var i = 0; i < floatCount; i += 2)
            {
                bone.localToWorld(floats[i], floats[i + 1], out float toX, out float toY);
                var point =  transform.TransformPoint(toX, toY, transform.position.z);
                _verts.Add(point);
            }


            var minX = 0f;
            var minY = 0f;
            var maxX = 0f;
            var maxY = 0f;
            for (int i = 0; i < _verts.Count; i++)
            {
                //var worldPos= transform.TransformPoint(_verts[i]);
                var worldPos = _verts[i];
                if (i == 0)
                {
                    minX = worldPos.x;
                    maxX = minX;
                    minY = worldPos.y;
                    maxY = minY;
                }
                else
                {
                    minX = Math.Min(minX, worldPos.x);
                    maxX = Math.Max(maxX, worldPos.x);
                    minY = Math.Min(minY, worldPos.y);
                    maxY = Math.Max(maxY, worldPos.y);
                }
            }
            
            Rect rt = new Rect(minX, minY, maxX - minX, maxY - minY);
            return rt;
        }

        Debug.LogError($"Cant find slot name {slotName} at skeleton ");
        return Rect.zero;
    }

    public Bone FindBone(string boneName)
    {
        return skeleton.FindBone(boneName);
    }

    /// <summary>
    /// 寻找骨骼世界坐标
    /// </summary>
    /// <param name="boneName"></param>
    /// <returns></returns>
    public Vector3 FindBonePos(string boneName)
    {
        var bone = FindBone(boneName);
        return transform.TransformPoint(bone.X, bone.Y, 0);
    }

    /// <summary>
    /// 获取多边形碰撞
    /// </summary>
    /// <param name="slotName"></param>
    /// <returns></returns>
    public Vector2[] GetCollisionWithName(string slotName)
    {
        _verts.Clear();
        var bone = skeleton.FindSlot(slotName)?.Bone;
        var attachment = skeleton.GetAttachment(slotName, slotName);

        if (attachment == null || bone == null)
        {
            return null;
        }

        if (!(attachment is BoundingBoxAttachment boundingBox)) return null;
        var floats = boundingBox.Vertices;
        var floatCount = floats.Length;
        //计算缩放对坐标造成的影响
        for (var i = 0; i < floatCount; i += 2)
        {
          
            bone.localToWorld(floats[i], floats[i + 1], out float toX, out float toY);
            _verts.Add(transform.TransformPoint(
                new Vector3(toX,toY,0)));
        }

        return _verts.ToArray();
    }

    public virtual void Update()
    {
        Update(Time.deltaTime);
    }

    public virtual void Update(float deltaTime)
    {
        if (!valid)
            return;

        deltaTime *= timeScale;
        skeleton.Update(deltaTime);
        state.Update(deltaTime);
        state.Apply(skeleton);

        if (_UpdateLocal != null)
            _UpdateLocal(this);

        skeleton.UpdateWorldTransform();

        if (_UpdateWorld != null)
        {
            _UpdateWorld(this);
            skeleton.UpdateWorldTransform();
        }

        if (_UpdateComplete != null)
        {
            _UpdateComplete(this);
        }
    }
}