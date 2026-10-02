#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace InvisibilityPotion.Dev
{
    /// <summary>
    /// ip_exportmesh (plan 4 fix round 2, ruling 7): Wavefront OBJ references for fitting models in Blender, written to
    /// &lt;game&gt;/BepInEx/export/ (outside the repository).
    ///   ip_exportmesh &lt;prefab&gt;: every MeshFilter (and SkinnedMeshRenderer, unskinned) mesh of the prefab in the prefab root's
    ///                             space, metres.
    ///   ip_exportmesh head:       the local player's body (VisEquipment.m_bodyModel) baked in its current pose (BakeMesh), in the
    ///                             space of the helmet joint (VisEquipment.m_helmet, the transform AttachItem parents a helmet's
    ///                             "attach" to), plus the vanilla HelmetLeather "attach" meshes in the same space.
    /// Axes: Unity is left-handed, OBJ right-handed: x is negated and the triangle winding reversed (the usual Unity OBJ export),
    /// so Blender's OBJ import (forward -Z, up Y) shows the model the right way round. Meshes that are not CPU-readable (bundle
    /// meshes are imported with read/write off) are read back from their GPU vertex and index buffers.
    /// </summary>
    internal static class MeshExport
    {
        public static void Register() => CommandManager.Instance.AddConsoleCommand(new ExportMeshCommand());

        private static string ExportDir => System.IO.Path.Combine(Paths.BepInExRootPath, "export");

        private class ExportMeshCommand : ConsoleCommand
        {
            public override string Name => "ip_exportmesh";
            public override string Help => "ip_exportmesh <prefab|head>: write OBJ files to BepInEx/export/ (prefab: all meshes in the prefab's space; " +
                                           "head: your body baked in the helmet joint's space plus the HelmetLeather attach mesh)";

            public override void Run(string[] args)
            {
                if (args.Length < 1) { DevCommands.Say(Help); return; }
                try
                {
                    Directory.CreateDirectory(ExportDir);
                    if (string.Equals(args[0], "head", StringComparison.OrdinalIgnoreCase)) ExportHead();
                    else ExportPrefab(args[0]);
                }
                catch (Exception e) { DevCommands.Say($"ip_exportmesh failed: {e}"); }
            }

            public override List<string> CommandOptionList()
            {
                var list = new List<string> { "head" };
                if (ZNetScene.instance != null) list.AddRange(ZNetScene.instance.GetPrefabNames());
                return list;
            }
        }

        private static void ExportPrefab(string name)
        {
            var go = AssetCommands.FindPrefab(name);
            if (go == null) { DevCommands.Say($"ip_exportmesh: unknown prefab {name}"); return; }
            var obj = new ObjWriter();
            var toRoot = go.transform.worldToLocalMatrix;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var r = mf.GetComponent<Renderer>();
                obj.Add($"{HierarchyPath(mf.transform, go.transform)}{(r != null && !r.enabled ? "_disabled" : "")}", mf.sharedMesh, toRoot * mf.transform.localToWorldMatrix);
            }
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) obj.Add(HierarchyPath(smr.transform, go.transform) + "_unskinned", smr.sharedMesh, toRoot * smr.transform.localToWorldMatrix);
            Write($"{go.name}.obj", obj, $"prefab {go.name}, root space, metres");
        }

        private static void ExportHead()
        {
            var p = Player.m_localPlayer;
            if (p == null) { DevCommands.Say("ip_exportmesh head: no local player"); return; }
            var vis = p.GetComponent<VisEquipment>();
            if (vis == null || vis.m_bodyModel == null || vis.m_helmet == null) { DevCommands.Say("ip_exportmesh head: no VisEquipment body model or helmet joint"); return; }
            var joint = vis.m_helmet;
            var smr = vis.m_bodyModel;
            var toJoint = joint.worldToLocalMatrix;
            DevCommands.Say($"ip_exportmesh head: helmet joint '{joint.name}' under '{(joint.parent != null ? joint.parent.name : "-")}', world {joint.position:F3}, " +
                            $"lossy scale {joint.lossyScale:F3}; body '{smr.name}' mesh '{(smr.sharedMesh != null ? smr.sharedMesh.name : "-")}'");

            var body = new ObjWriter();
            var baked = new Mesh { name = "ip_baked_body" };
            string how;
            try
            {
                // useScale false: vertices as if the renderer's scale were 1, so localToWorldMatrix maps them to the world.
                smr.BakeMesh(baked, false);
                if (baked.vertexCount == 0) throw new InvalidOperationException("BakeMesh produced no vertices");
                body.Add("body_baked", baked, toJoint * smr.transform.localToWorldMatrix);
                how = "BakeMesh (current pose)";
            }
            catch (Exception e)
            {
                // Bind pose fallback: bindposes[i] maps mesh space to bone i's local space at bind time; the joint hangs at a fixed
                // offset below its nearest skinned ancestor bone, so joint space = joint.worldToLocal * bone.localToWorld * bindpose.
                DevCommands.Say($"ip_exportmesh head: BakeMesh failed ({e.Message}); exporting the bind pose instead");
                var (bone, index) = SkinnedAncestor(smr, joint);
                if (bone == null || smr.sharedMesh == null || index >= smr.sharedMesh.bindposes.Length) { DevCommands.Say("ip_exportmesh head: no skinned ancestor bone of the helmet joint"); return; }
                body.Add("body_bindpose", smr.sharedMesh, toJoint * bone.localToWorldMatrix * smr.sharedMesh.bindposes[index]);
                how = $"bind pose via bone '{bone.name}'";
            }
            finally { UnityEngine.Object.Destroy(baked); }
            Write("head_body.obj", body, $"local player body ({how}) in the space of the helmet joint '{joint.name}', metres");

            var helmet = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("HelmetLeather") : null;
            var attach = helmet != null ? helmet.transform.Find("attach") ?? helmet.transform.Find("attach_skin") : null;
            if (attach == null) { DevCommands.Say("ip_exportmesh head: HelmetLeather or its attach child not found; reference skipped"); return; }
            // AttachItem puts the attach object at the joint with identity local position/rotation: attach space = joint space.
            var refObj = new ObjWriter();
            var toAttach = attach.worldToLocalMatrix;
            foreach (var mf in attach.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) refObj.Add(HierarchyPath(mf.transform, attach), mf.sharedMesh, toAttach * mf.transform.localToWorldMatrix);
            foreach (var s in attach.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh != null) refObj.Add(HierarchyPath(s.transform, attach) + "_unskinned", s.sharedMesh, toAttach * s.transform.localToWorldMatrix);
            Write("head_HelmetLeather.obj", refObj, $"HelmetLeather/{attach.name} in its own space (= the helmet joint's space when attached), metres");
        }

        /// <summary>The nearest ancestor of <paramref name="t"/> (itself included) that is one of the renderer's bones, with its index.</summary>
        private static (Transform, int) SkinnedAncestor(SkinnedMeshRenderer smr, Transform t)
        {
            var bones = smr.bones;
            for (var a = t; a != null; a = a.parent)
            {
                var i = Array.IndexOf(bones, a);
                if (i >= 0) return (a, i);
            }
            return (null, -1);
        }

        private static string HierarchyPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var a = t; a != null && a != root; a = a.parent) parts.Insert(0, a.name);
            return parts.Count == 0 ? root.name : string.Join("/", parts);
        }

        private static void Write(string file, ObjWriter obj, string what)
        {
            var path = System.IO.Path.Combine(ExportDir, file);
            File.WriteAllText(path, obj.Text(what));
            DevCommands.Say($"ip_exportmesh: wrote {path} ({what}): {obj.Summary}");
            foreach (var w in obj.Warnings) DevCommands.Say($"  {w}");
        }

        /// <summary>Accumulates OBJ objects: v lines with x negated, faces with reversed winding, 1-based running indices.</summary>
        private sealed class ObjWriter
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private int _vertexBase = 1, _objects, _vertices, _triangles;
            public readonly List<string> Warnings = new List<string>();

            public string Summary => $"{_objects} object(s), {_vertices} vertices, {_triangles} triangles";

            public void Add(string name, Mesh mesh, Matrix4x4 toSpace)
            {
                if (!TryRead(mesh, out var verts, out var tris, out var how))
                {
                    Warnings.Add($"{name}: mesh '{mesh.name}' not exported ({how})");
                    return;
                }
                _sb.Append("o ").Append(name.Replace(' ', '_')).Append('\n');
                _sb.Append("# mesh '").Append(mesh.name).Append("', read ").Append(how).Append('\n');
                foreach (var v in verts)
                {
                    var p = toSpace.MultiplyPoint3x4(v);
                    _sb.Append("v ").Append(F(-p.x)).Append(' ').Append(F(p.y)).Append(' ').Append(F(p.z)).Append('\n');
                }
                for (var i = 0; i + 2 < tris.Count; i += 3)
                {
                    _sb.Append("f ").Append(tris[i] + _vertexBase).Append(' ').Append(tris[i + 2] + _vertexBase).Append(' ').Append(tris[i + 1] + _vertexBase).Append('\n');
                    _triangles++;
                }
                _vertexBase += verts.Length;
                _vertices += verts.Length;
                _objects++;
            }

            public string Text(string what) => $"# InvisibilityPotion ip_exportmesh: {what}\n# x negated, winding reversed (Unity left-handed -> OBJ right-handed)\n{_sb}";

            private static string F(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>Positions and triangle indices (all triangle submeshes, baseVertex applied) from the CPU copy or the GPU buffers.</summary>
        private static bool TryRead(Mesh mesh, out Vector3[] verts, out List<int> tris, out string how)
        {
            verts = null;
            tris = new List<int>();
            try
            {
                if (mesh.isReadable)
                {
                    verts = mesh.vertices;
                    for (var s = 0; s < mesh.subMeshCount; s++)
                    {
                        if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                        tris.AddRange(mesh.GetTriangles(s, true));
                    }
                    how = "CPU";
                    return true;
                }
                if (mesh.GetVertexAttributeDimension(VertexAttribute.Position) < 3) { how = "no 3D position attribute"; return false; }
                var stream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
                var offset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
                var format = mesh.GetVertexAttributeFormat(VertexAttribute.Position);
                byte[] vdata;
                int stride;
                using (var vb = mesh.GetVertexBuffer(stream))
                {
                    stride = vb.stride;
                    vdata = new byte[vb.count * vb.stride];
                    vb.GetData(vdata);
                }
                var n = mesh.vertexCount;
                verts = new Vector3[n];
                for (var i = 0; i < n; i++)
                {
                    var o = i * stride + offset;
                    switch (format)
                    {
                        case VertexAttributeFormat.Float32:
                            verts[i] = new Vector3(BitConverter.ToSingle(vdata, o), BitConverter.ToSingle(vdata, o + 4), BitConverter.ToSingle(vdata, o + 8));
                            break;
                        case VertexAttributeFormat.Float16:
                            verts[i] = new Vector3(Mathf.HalfToFloat(BitConverter.ToUInt16(vdata, o)), Mathf.HalfToFloat(BitConverter.ToUInt16(vdata, o + 2)), Mathf.HalfToFloat(BitConverter.ToUInt16(vdata, o + 4)));
                            break;
                        default:
                            how = $"position format {format} not supported";
                            return false;
                    }
                }
                byte[] idata;
                using (var ib = mesh.GetIndexBuffer())
                {
                    idata = new byte[ib.count * ib.stride];
                    ib.GetData(idata);
                }
                var wide = mesh.indexFormat == IndexFormat.UInt32;
                for (var s = 0; s < mesh.subMeshCount; s++)
                {
                    var d = mesh.GetSubMesh(s);
                    if (d.topology != MeshTopology.Triangles) continue;
                    for (var k = 0; k < d.indexCount; k++)
                    {
                        var at = (d.indexStart + k) * (wide ? 4 : 2);
                        var idx = wide ? (int)BitConverter.ToUInt32(idata, at) : BitConverter.ToUInt16(idata, at);
                        tris.Add(idx + d.baseVertex);
                    }
                }
                how = $"GPU buffers ({format})";
                return true;
            }
            catch (Exception e)
            {
                how = $"read failed: {e.Message}";
                return false;
            }
        }
    }
}
#endif
