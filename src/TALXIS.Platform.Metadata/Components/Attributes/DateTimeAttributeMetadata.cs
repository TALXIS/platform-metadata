namespace TALXIS.Platform.Metadata.Components.Attributes;

public sealed class DateTimeAttributeMetadata : AttributeMetadata
{
    public override AttributeType AttributeType => AttributeType.DateTime;
    public DateTimeFormat Format { get; set; } = DateTimeFormat.DateAndTime;
    public DateTimeBehavior DateTimeBehavior { get; set; } = DateTimeBehavior.UserLocal;
}

public enum DateTimeFormat { DateOnly, DateAndTime }

public enum DateTimeBehavior { UserLocal, DateOnly, TimeZoneIndependent }
