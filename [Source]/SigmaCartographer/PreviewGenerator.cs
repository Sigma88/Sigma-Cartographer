using System;
using System.Collections.Generic;
using UnityEngine;


namespace SigmaCartographerPlugin
{
    internal static class PreviewGenerator
    {
        private struct ProjectionPlane
        {
            private readonly Vector3 m_Normal;
            private readonly Single m_Distance;

            ProjectionPlane(Vector3 inNormal, Vector3 inPoint)
            {
                m_Normal = Vector3.Normalize(inNormal);
                m_Distance = -Vector3.Dot(inNormal, inPoint);
            }

            internal Vector3 ClosestPointOnPlane(Vector3 point)
            {
                Single d = Vector3.Dot(m_Normal, point) + m_Distance;
                return point - m_Normal * d;
            }

            internal Single GetDistanceToPoint(Vector3 point)
            {
                Single signedDistance = Vector3.Dot(m_Normal, point) + m_Distance;
                if (signedDistance < 0f)
                    signedDistance = -signedDistance;

                return signedDistance;
            }
        }

        private class CameraSetup
        {
            private Vector3 position;
            private Quaternion rotation;

            private RenderTexture targetTexture;

            private Color backgroundColor;
            private Boolean orthographic;
            private Single orthographicSize;
            private Single nearClipPlane;
            private Single farClipPlane;
            private Single aspect;
            private CameraClearFlags clearFlags;

            internal void GetSetup(Camera camera)
            {
                position = camera.transform.position;
                rotation = camera.transform.rotation;

                targetTexture = camera.targetTexture;

                backgroundColor = camera.backgroundColor;
                orthographic = camera.orthographic;
                orthographicSize = camera.orthographicSize;
                nearClipPlane = camera.nearClipPlane;
                farClipPlane = camera.farClipPlane;
                aspect = camera.aspect;
                clearFlags = camera.clearFlags;
            }

            internal void ApplySetup(Camera camera)
            {
                camera.transform.position = position;
                camera.transform.rotation = rotation;

                camera.targetTexture = targetTexture;

                camera.backgroundColor = backgroundColor;
                camera.orthographic = orthographic;
                camera.orthographicSize = orthographicSize;
                camera.nearClipPlane = nearClipPlane;
                camera.farClipPlane = farClipPlane;
                camera.aspect = aspect;
                camera.clearFlags = clearFlags;

                targetTexture = null;
            }
        }

        private const Int32 PREVIEW_LAYER = 22;
        private static Vector3 PREVIEW_POSITION = new Vector3(-9245f, 9899f, -9356f);

        private static Camera renderCamera;
        private static CameraSetup cameraSetup = new CameraSetup();

        private static List<Renderer> renderersList = new List<Renderer>(64);
        private static List<Int32> layersList = new List<Int32>(64);

        private static Single aspect;
        private static Single minX, maxX, minY, maxY;

        private static Vector3 boundsCenter;

        private static Camera m_internalCamera;

        private static Camera InternalCamera
        {
            get
            {
                if (m_internalCamera == null)
                {
                    m_internalCamera = new GameObject("ModelPreviewGeneratorCamera").AddComponent<Camera>();
                    m_internalCamera.enabled = false;
                    m_internalCamera.nearClipPlane = 0.01f;
                    m_internalCamera.cullingMask = 1 << PREVIEW_LAYER;
                    m_internalCamera.gameObject.hideFlags = HideFlags.HideAndDontSave;
                }

                return m_internalCamera;
            }
        }

        private static Camera m_previewRenderCamera;

        static Camera PreviewRenderCamera
        {
            get { return m_previewRenderCamera; }
            set { m_previewRenderCamera = value; }
        }

        private static Color m_backgroundColor;

        internal static Color BackgroundColor
        {
            get { return m_backgroundColor; }
            set { m_backgroundColor = value; }
        }

        private static double m_LAToffset;

        internal static double LAToffset
        {
            get { return m_LAToffset; }
            set { m_LAToffset = value; }
        }

        private static double m_LONoffset;

        internal static double LONoffset
        {
            get { return m_LONoffset; }
            set { m_LONoffset = value; }
        }

        static PreviewGenerator()
        {
            PreviewRenderCamera = null;
            BackgroundColor = Color.clear;
        }

        internal static Texture2D GenerateModelPreview(Transform model, Int32 width = 64, Int32 height = 64, Boolean shouldCloneModel = false)
        {
            return GenerateModelPreviewWithShader(model, null, null, width, height, shouldCloneModel);
        }

        internal static Texture2D GenerateModelPreviewWithShader(Transform model, Shader shader, String replacementTag, Int32 width = 64, Int32 height = 64, Boolean shouldCloneModel = false)
        {
            if (model == null || model.Equals(null))
                return null;


            // The Light He called Day
            GameObject lightObject = new GameObject();
            Light light = lightObject.AddOrGetComponent<Light>();
            lightObject.transform.position = new Vector3(17.2469177246094f, 56.2267150878906f, -36.3499984741211f);
            lightObject.transform.rotation = new Quaternion(0.1f, 0.1f, -0.7f, -0.7f);
            light.intensity = 1.5f;
            light.shadowBias = 0.047f;
            light.shadows = LightShadows.Soft;
            light.type = LightType.Directional;


            Texture2D result = null;

            if (!model.gameObject.scene.IsValid() || !model.gameObject.scene.isLoaded)
                shouldCloneModel = true;

            Transform previewObject;
            if (shouldCloneModel)
            {
                previewObject = UnityEngine.Object.Instantiate(model, null, false);
                previewObject.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                previewObject = model;

                layersList.Clear();
                GetLayerRecursively(previewObject);
            }

            Boolean wasActive = previewObject.gameObject.activeSelf;
            Vector3 prevPos = previewObject.position;
            Quaternion prevRot = previewObject.rotation;

            try
            {
                SetupCamera();
                SetLayerRecursively(previewObject);

                if (!wasActive)
                    previewObject.gameObject.SetActive(true);

                Vector3 previewDir = previewObject.rotation * Vector3.forward;

                renderersList.Clear();
                previewObject.GetComponentsInChildren(renderersList);

                Bounds previewBounds = new Bounds();
                Boolean init = false;
                for (Int32 i = 0; i < renderersList.Count; i++)
                {
                    if (!renderersList[i].enabled)
                        continue;

                    if (!init)
                    {
                        previewBounds = renderersList[i].bounds;
                        init = true;
                    }
                    else
                        previewBounds.Encapsulate(renderersList[i].bounds);
                }

                if (!init)
                {
                    UnityEngine.Object.DestroyImmediate(lightObject);
                    return null;
                }

                boundsCenter = previewBounds.center;
                Vector3 boundsExtents = previewBounds.extents;
                Vector3 boundsSize = 2f * boundsExtents;
                Vector3 up = previewObject.up;


                previewObject.Rotate(-previewObject.right, (float)LAToffset);
                previewObject.Rotate(up, (float)LONoffset);


                aspect = (Single)width / height;
                renderCamera.aspect = aspect;
                renderCamera.transform.rotation = Quaternion.LookRotation(previewDir, up);


                renderCamera.transform.position = boundsCenter;

                minX = minY = Mathf.Infinity;
                maxX = maxY = Mathf.NegativeInfinity;

                Vector3 point = boundsCenter + boundsExtents;
                ProjectBoundingBoxMinMax(point);
                point.x -= boundsSize.x;
                ProjectBoundingBoxMinMax(point);
                point.y -= boundsSize.y;
                ProjectBoundingBoxMinMax(point);
                point.x += boundsSize.x;
                ProjectBoundingBoxMinMax(point);
                point.z -= boundsSize.z;
                ProjectBoundingBoxMinMax(point);
                point.x -= boundsSize.x;
                ProjectBoundingBoxMinMax(point);
                point.y += boundsSize.y;
                ProjectBoundingBoxMinMax(point);
                point.x += boundsSize.x;
                ProjectBoundingBoxMinMax(point);

                Single distance = boundsExtents.magnitude + 1f;
                renderCamera.orthographicSize = Mathf.Max(maxY - minY, (maxX - minX) / aspect) * 0.5f;




                renderCamera.transform.position = boundsCenter - previewDir * distance;
                renderCamera.farClipPlane = distance * 4f;

                RenderTexture temp = RenderTexture.active;
                RenderTexture renderTex = RenderTexture.GetTemporary(width, height, 16);
                RenderTexture.active = renderTex;
                if (BackgroundColor.a < 1)
                    GL.Clear(false, true, BackgroundColor);

                renderCamera.targetTexture = renderTex;

                if (shader == null)
                    renderCamera.Render();
                else
                    renderCamera.RenderWithShader(shader, replacementTag ?? String.Empty);

                renderCamera.targetTexture = null;

                result = new Texture2D(width, height, BackgroundColor.a < 1 ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                result.Apply(false, false);

                RenderTexture.active = temp;
                RenderTexture.ReleaseTemporary(renderTex);
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
            finally
            {
                if (shouldCloneModel)
                    UnityEngine.Object.DestroyImmediate(previewObject.gameObject);
                else
                {
                    if (!wasActive)
                        previewObject.gameObject.SetActive(false);

                    Int32 index = 0;
                    SetLayerRecursively(previewObject, ref index);
                }

                if (renderCamera == m_previewRenderCamera)
                    cameraSetup.ApplySetup(renderCamera);
            }


            // And the Darkness He called Night
            UnityEngine.Object.DestroyImmediate(lightObject);

            return result;
        }

        private static void SetupCamera()
        {
            if (m_previewRenderCamera != null && !m_previewRenderCamera.Equals(null))
            {
                cameraSetup.GetSetup(m_previewRenderCamera);

                renderCamera = m_previewRenderCamera;
                renderCamera.nearClipPlane = 0.01f;
            }
            else
                renderCamera = InternalCamera;

            renderCamera.backgroundColor = m_backgroundColor;
            renderCamera.orthographic = true;
            renderCamera.clearFlags = BackgroundColor.a < 1 ? CameraClearFlags.Depth : CameraClearFlags.Color;
        }

        private static void ProjectBoundingBoxMinMax(Vector3 point)
        {
            Vector3 localPoint = renderCamera.transform.InverseTransformPoint(point);
            if (localPoint.x < minX)
                minX = localPoint.x;
            if (localPoint.x > maxX)
                maxX = localPoint.x;
            if (localPoint.y < minY)
                minY = localPoint.y;
            if (localPoint.y > maxY)
                maxY = localPoint.y;
        }

        private static void SetLayerRecursively(Transform obj)
        {
            obj.gameObject.layer = PREVIEW_LAYER;
            for (Int32 i = 0; i < obj.childCount; i++)
                SetLayerRecursively(obj.GetChild(i));
        }

        private static void GetLayerRecursively(Transform obj)
        {
            layersList.Add(obj.gameObject.layer);
            for (Int32 i = 0; i < obj.childCount; i++)
                GetLayerRecursively(obj.GetChild(i));
        }

        private static void SetLayerRecursively(Transform obj, ref Int32 index)
        {
            obj.gameObject.layer = layersList[index++];
            for (Int32 i = 0; i < obj.childCount; i++)
                SetLayerRecursively(obj.GetChild(i), ref index);
        }
    }
}
