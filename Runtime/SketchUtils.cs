using UnityEngine;

public static class SketchUtils
{
   public static Matrix4x4 GetScreenToWorldMatrix(Camera camera)
   {
      var planeDistance = (camera.nearClipPlane + camera.farClipPlane) * 0.5f;
      var transform = camera.transform;
      var forward = transform.forward;
      var origin = transform.TransformPoint(0f, 0f, planeDistance);
      var width = camera.pixelWidth;
      var height = camera.pixelHeight;
      var scale = 1f;
      if (camera.orthographic) 
         scale = 2f * camera.orthographicSize / height;
      else
      {
         var vFovHalfRad = camera.fieldOfView * Mathf.Deg2Rad * 0.5f;
         var halfYSize = (float)(planeDistance * Mathf.Tan(vFovHalfRad));
         scale = (float)(2f * halfYSize / height );
      }
      var rightScale = transform.right * scale;
      var upScale = transform.up * scale;
      var frwScale = forward * scale;
      origin += rightScale * ( -width / 2f ) + upScale * ( -height / 2f );
      return new Matrix4x4(rightScale, upScale, frwScale, new Vector4(origin.x, origin.y, origin.z, 1));
   }

   // public static Matrix4x4 GetScreenToWorldMatrixOrtho(Camera camera)
   // {
   //    // Calculate half of the orthographic size
   //    float halfSize = camera.orthographicSize;

   //    // Calculate aspect ratio
   //    float aspect = camera.aspect;

   //    // Calculate the scale factors for the x and y axes
   //    float scaleX = halfSize * aspect * 2 / camera.pixelWidth;
   //    float scaleY = halfSize * 2 / camera.pixelHeight;

   //    // Create a transformation matrix move the origin to the bottom left corner
   //    Matrix4x4 transformation = 
   //       Matrix4x4.Translate(camera.transform.position) *
   //       Matrix4x4.Rotate(camera.transform.rotation) *
   //       Matrix4x4.Translate(new Vector3(-halfSize * aspect, -halfSize, camera.nearClipPlane));

   //    // Create a scaling matrix to scale the x and y coordinates
   //    Matrix4x4 scale = Matrix4x4.Scale(new Vector3(scaleX, scaleY, 1));

   //    // Combine the translation and scaling matrices
   //    Matrix4x4 matrix = transformation * scale;

   //    return matrix;
   // }

   public static Matrix4x4 GetScreenToWorldMatrixPerspective(Camera camera, float depth)
   {
      // Inverse View Matrix
      var inverseViewMatrix = camera.cameraToWorldMatrix;
      // Inverse Projection Matrix
      var inverseProjectionMatrix = camera.projectionMatrix.inverse;
      // Screen to NDC transformation
      var screenToNDC = Matrix4x4.identity;
      screenToNDC.m00 = 2.0f / camera.pixelWidth;
      screenToNDC.m11 = 2.0f / camera.pixelHeight;
      screenToNDC.m03 = -1;
      screenToNDC.m13 = -1;
      screenToNDC.m22 = 1;
      screenToNDC.m23 = 0;
      
      // Depth adjustment (moving from NDC to a specific depth in camera space)
      var depthAdjustment = Matrix4x4.identity;
      depthAdjustment.m22 = depth;
      // Combine matrices
      // Note: The actual combination can depend on the specifics of the depth adjustment and how you plan to use z-values.
      var screenToWorldMatrix = inverseViewMatrix * inverseProjectionMatrix * depthAdjustment * screenToNDC;
      return screenToWorldMatrix;
   }
}