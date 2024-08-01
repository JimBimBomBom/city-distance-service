using FluentMigrator;

[Migration(202408011500)]
public class InitialCreate : Migration
{
    public override void Up()
    {
        if (!Schema.Table("cities").Exists())
        {
            Create.Table("cities")
                .WithColumn("CityId").AsInt32().PrimaryKey()
                .WithColumn("CityName").AsString(100).NotNullable()
                .WithColumn("Latitude").AsDecimal(10, 8).NotNullable()
                .WithColumn("Longitude").AsDecimal(10, 8).NotNullable();
        }
    }

    public override void Down()
    {
        Delete.Table("cities");
    }
}

[Migration(202408011542)]
public class ChangeCityIdToGuid : Migration
{
    public override void Up()
    {
        Alter.Table("cities").AddColumn("TempCityId").AsGuid().WithDefaultValue(Guid.NewGuid());

        Execute.Sql("UPDATE cities SET TempCityId = UUID()");

        Delete.Column("CityId").FromTable("cities");
        Rename.Column("TempCityId").OnTable("cities").To("CityId");

        Alter.Column("Latitude").OnTable("cities").AsDecimal(10, 8).NotNullable();
        Alter.Column("Longitude").OnTable("cities").AsDecimal(10, 8).NotNullable();
    }

    public override void Down()
    {
    }
}