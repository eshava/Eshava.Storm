using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;

namespace Eshava.Storm.TestApp
{
	public class Program
	{
		private const string DATAMODEL = "dataModel";
		private const string DOMAINMODEL = "domainModel";
		private const string MODEL = "model";
		private const string MODELPROPERTY = "modelProperty";
		private const string MODELPROPERTYATTRIBUTE = "modelPropertyAttribute";
		private const string MODELPROPERTYATTRIBUTEPARAMETER = "modelPropertyAttributeParameter";
		private const string MODELPROPERTYVALIDATIONRULE = "modelPropertyValidationRule";
		private const string MODELPROVIDERSERVICECONSTRUCTORPARAMETER = "modelProviderServiceConstructorParameter";
		private const string SERVICEPROJECTNAMESPACEMODEL = "serviceProjectNamespaceModel";
		private const string SERVICEPROJECTNAMESPACEMODELSERVICEPROJECTNAMESPACE = "serviceProjectNamespaceModelServiceProjectNamespace";

		public static async Task Main(string[] args)
		{
			var connectionString = "Data Source=eshava-mssql-dev;Initial Catalog=Eshava.Ascent.Account-13;User Id=eshava-ascent;Password=k]Cj.g3SW[#ckaI.rOJk;Encrypt=false;";

			//Eshava.Storm.Settings.EnableValueReadingBasedOnTableAliasOccurrence = true;

			var query = """
				SELECT
					 dataModel.DisplayName
					,model.CreateProviderService
					,model.CreateRepository
					,model.DisplayName
					,model.EntityId
					,model.Id
					,model.IdentifierGenerationOnAdd
					,model.IdentifierType
					,model.Name
					,model.TableName
					,modelProperty.AddToCreationBag
					,modelProperty.Id
					,modelProperty.IsEnumReference
					,modelProperty.IsReference
					,modelProperty.Name
					,modelProperty.SkipFromDomainModel
					,modelProperty.Type
					,modelProperty.UsingForType
					,modelProviderServiceConstructorParameter.DataModelId
					,modelProviderServiceConstructorParameter.Id
					,modelProviderServiceConstructorParameter.IsCustomParameter
					,modelProviderServiceConstructorParameter.Name
					,modelProviderServiceConstructorParameter.Type
					,modelProviderServiceConstructorParameter.UsingForType
				FROM
					 modeling.Models model
				LEFT JOIN
					 modeling.ModelProperties modelProperty
						ON modelProperty.ModelId = model.Id
						AND modelProperty.Status = 1
				LEFT JOIN
					 modeling.ModelProviderServiceConstructorParameters modelProviderServiceConstructorParameter
						ON modelProviderServiceConstructorParameter.ModelId = model.Id
						AND modelProviderServiceConstructorParameter.Status = 1
				LEFT JOIN
					 modeling.Models dataModel
						ON dataModel.Id = modelProviderServiceConstructorParameter.DataModelId
						AND dataModel.Status = 1
				WHERE
					model.Id = 11
				 AND
				 	model.ModelTypeId = @ModelTypeId
					
				AND
					model.Status = 1

				""";

			using (var connection = new SqlConnection(connectionString))
			{
				var result = await connection.QueryAsync(query, mapper =>
					{
						var dtoMap = mapper.Map<ModelReadDataModelDataModelDto>(MODEL);
						var dtoPropMap = mapper.Map<ModelReadDataModelDataModelPropertyDto>(MODELPROPERTY);
						var dtoCtorMap = mapper.Map<ModelReadDataModelDataModelProviderServiceConstructorParameterDto>(MODELPROVIDERSERVICECONSTRUCTORPARAMETER);


						var dto = new ModelReadDataModelDataModelDto
						{
							CreateProviderService = mapper.GetValue<bool>("CreateProviderService", MODEL),
							CreateRepository = mapper.GetValue<bool>("CreateRepository", MODEL),
							DisplayName = mapper.GetValue<string>("DisplayNam", MODEL),
							EntityId = mapper.GetValue<int>("EntityId", MODEL),
							Id = mapper.GetValue<int>("Id", MODEL),
							IdentifierGenerationOnAdd = mapper.GetValue<bool>("IdentifierGenerationOnAdd", MODEL),
							IdentifierType = mapper.GetValue<string>("IdentifierType", MODEL),
							Name = mapper.GetValue<string>("Name", MODEL),
							TableName = mapper.GetValue<string>("TableName", MODEL),
							Properties = mapper.GetValue<int>("Id", MODELPROPERTY) == default ? new List<ModelReadDataModelDataModelPropertyDto>() : new List<ModelReadDataModelDataModelPropertyDto>
							{
								new ModelReadDataModelDataModelPropertyDto
								{
									AddToCreationBag = mapper.GetValue<bool>("AddToCreationBag", MODELPROPERTY),
									Id = mapper.GetValue<int>("Id", MODELPROPERTY),
									IsEnumReference = mapper.GetValue<bool>("IsEnumReference", MODELPROPERTY),
									IsReference = mapper.GetValue<bool>("IsReference", MODELPROPERTY),
									Name = mapper.GetValue<string>("Name", MODELPROPERTY),
									SkipFromDomainModel = mapper.GetValue<bool>("SkipFromDomainModel", MODELPROPERTY),
									Type = mapper.GetValue<string>("Type", MODELPROPERTY),
									UsingForType = mapper.GetValue<string>("UsingForType", MODELPROPERTY)
								}
							},
							ProviderServiceConstructorParameters = mapper.GetValue<int>("Id", MODELPROVIDERSERVICECONSTRUCTORPARAMETER) == default ? new List<ModelReadDataModelDataModelProviderServiceConstructorParameterDto>() : new List<ModelReadDataModelDataModelProviderServiceConstructorParameterDto>
							{
								new ModelReadDataModelDataModelProviderServiceConstructorParameterDto
								{
									DataModel = mapper.GetValue<string>("DisplayName", DATAMODEL),
									DataModelId = mapper.GetValue<int?>("DataModelId", MODELPROVIDERSERVICECONSTRUCTORPARAMETER),
									Id = mapper.GetValue<int>("Id", MODELPROVIDERSERVICECONSTRUCTORPARAMETER),
									IsCustomParameter = mapper.GetValue<bool>("IsCustomParameter", MODELPROVIDERSERVICECONSTRUCTORPARAMETER),
									Name = mapper.GetValue<string>("Name", MODELPROVIDERSERVICECONSTRUCTORPARAMETER),
									Type = mapper.GetValue<string>("Type", MODELPROVIDERSERVICECONSTRUCTORPARAMETER),
									UsingForType = mapper.GetValue<string>("UsingForType", MODELPROVIDERSERVICECONSTRUCTORPARAMETER)
								}
							}
						};
						return dto;
					}, new { ModelId = 11, ModelTypeId = 28, Status = 1 });

				if (true)
				{

				}
			}
		}

		public partial class ModelReadDataModelDataModelDto
		{
			public int Id { get; set; }
			public string Name { get; set; }
			public string DisplayName { get; set; }
			public int EntityId { get; set; }
			public string IdentifierType { get; set; }
			public bool IdentifierGenerationOnAdd { get; set; }
			public string TableName { get; set; }
			public bool CreateRepository { get; set; }
			public bool CreateProviderService { get; set; }
			public int? ParentDataModelId { get; set; }
			public string ParentDataModel { get; set; }
			public IEnumerable<ModelReadDataModelDataModelPropertyDto> Properties { get; set; }
			public IEnumerable<ModelReadDataModelDataModelProviderServiceConstructorParameterDto> ProviderServiceConstructorParameters { get; set; }
		}

		public partial class ModelReadDataModelDataModelPropertyDto
		{
			public int Id { get; set; }
			public string Name { get; set; }
			public string Type { get; set; }
			public string UsingForType { get; set; }
			public bool IsReference { get; set; }
			public int? ReferenceModelId { get; set; }
			public string ReferenceModel { get; set; }
			public bool SkipFromDomainModel { get; set; }
			public bool AddToCreationBag { get; set; }
			public bool IsEnumReference { get; set; }
			public int? EnumReferenceId { get; set; }
			public string EnumReference { get; set; }
		}

		public partial class ModelReadDataModelDataModelProviderServiceConstructorParameterDto
		{
			public int Id { get; set; }
			public bool IsCustomParameter { get; set; }
			public string Name { get; set; }
			public string Type { get; set; }
			public string UsingForType { get; set; }
			public int? DataModelId { get; set; }
			public string DataModel { get; set; }
		}
	}
}