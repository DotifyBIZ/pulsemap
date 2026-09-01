using System.Text.Json.Serialization;

namespace Pulsemap.App.Core.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ImagePlanSource), "image")]
[JsonDerivedType(typeof(RoomListSource), "roomList")]
public abstract class FloorPlanSource
{
}
