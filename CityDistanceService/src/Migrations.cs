using FluentMigrator;

[Migration(1)]
public class Create : Migration
{
    public override void Up()
    {
        if (!Schema.Table("cities").Exists())
        {
            Create
                .Table("cities")
                .WithColumn("CityId")
                .AsGuid()
                .PrimaryKey()
                .WithColumn("CityName")
                .AsString(100)
                .NotNullable()
                .WithColumn("Latitude")
                .AsDecimal(10, 8)
                .NotNullable()
                .WithColumn("Longitude")
                .AsDecimal(10, 8)
                .NotNullable();
        }
    }

    public override void Down()
    {
        Delete.Table("cities");
    }
}

[Migration(2)]
public class AddDefaultId : Migration
{
    public override void Up()
    {
        Alter
            .Table("cities")
            .AlterColumn("CityId")
            .AsGuid()
            .NotNullable()
            .WithDefaultValue(SystemMethods.NewGuid);
    }

    public override void Down()
    {
        Delete.Column("DefaultId").FromTable("cities");
    }
}

[Migration(3)]
public class AddCityNameIndex : Migration
{
    public override void Up()
    {
        Create.Index("IX_Cities_CityName").OnTable("cities").OnColumn("CityName").Unique();
    }

    public override void Down()
    {
        Delete.Index("IX_Cities_CityName").OnTable("cities");
    }
}
