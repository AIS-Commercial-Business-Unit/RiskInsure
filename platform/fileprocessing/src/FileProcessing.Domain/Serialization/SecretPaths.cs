namespace RiskInsure.FileProcessing.Domain.Serialization;

public class SecretPaths
{
    // In order for properties to be encrypted with Always Encrypted, 
    // they have to reside at the root of the JSON being sent to 
    // Cosmos. The JsonPath attribute is a hint for the converter to 
    // find them so that they can be mapped to the deeper properties
    // in our model classes.
    // 
    // WARNING: You can't change these values without creating a new 
    // CosmosDbContainer and migrating all of your data,
    // because CosmosDB doesn't support changing encyption paths after
    // a container has been created.  
    public const string FtpSecretPath = "/ftpProtocolSettings_password";
    public const string HttpsSecretPath = "/httpsProtocolSettings_passwordOrTokenOrApiKey";
    public const string AzureBlobSecretPath = "/azureBlobSettings_connectionString";    
}