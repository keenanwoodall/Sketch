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
}