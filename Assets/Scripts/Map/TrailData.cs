using System;
using System.Collections.Generic;
using Mapbox.Utils;
using UnityEngine;

[Serializable]
public class TrailData
{
    public List<TrailPoint> points = new List<TrailPoint>();
}

[Serializable]
public class TrailPoint
{
    public double latitude;
    public double longitude;
    public long timestamp;

    public TrailPoint(Vector2d latLon)
    {
        latitude = latLon.x;
        longitude = latLon.y;
        timestamp = DateTime.UtcNow.Ticks;
    }
    
    public Vector2d ToVector2d()
    {
        return new Vector2d(latitude, longitude);
    }
}
