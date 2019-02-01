#!/bin/bash

# variables
location="WestEurope"
storageAccountName="babofiles"
storageAccountResourceGroup="BaboFilesResourceGroup"
subscriptionName='BaboFilesLocalDebugging'
ngrockSubdomain="db1abac5"
functionName="ProcessBlobEvents"
endpointUrl="https://"$ngrockSubdomain".ngrok.io/runtime/webhooks/EventGrid?functionName="$functionName
deadLetterContainerName="deadletter"
filesContainerName="files"
subjectBeginsWith="/blobServices/default/containers/"$filesContainerName

# check if the storage account exists
echo "Checking if ["$storageAccountName"] storage account actually exists..."

set +e
(
    az storage account show --name $storageAccountName --resource-group $storageAccountResourceGroup &> /dev/null
)

if [ $? != 0 ]; then
	echo "No ["$storageAccountName"] storage account actually exists"
    set -e
    (
        # create the storage account
        az storage account create \
        --name $storageAccountName \
        --resource-group $storageAccountResourceGroup \
        --location $location \
        --sku Standard_LRS \
        --kind BlobStorage \
        --access-tier Hot 1> /dev/null
    )
    echo "["$storageAccountName"] storage account successfully created"
else
	echo "["$storageAccountName"] storage account already exists"
fi

# get storage account connection string
echo "Retrieving the connection string for ["$storageAccountName"] storage account..."
connectionString=$(az storage account show-connection-string --name $storageAccountName --resource-group $storageAccountResourceGroup --query connectionString --output tsv)

if [ -n $connectionString ]; then
    echo "The connection string for ["$storageAccountName"] storage account is ["$connectionString"]"
else
    echo "Failed to retrieve the connection string for ["$storageAccountName"] storage account"
    return
fi

# checking if deadletter container exists
echo "Checking if ["$deadLetterContainerName"] container already exists..."
set +e
(
    az storage container show --name $deadLetterContainerName --connection-string $connectionString &> /dev/null
)

if [ $? != 0 ]; then
	echo "No ["$deadLetterContainerName"] container actually exists in ["$storageAccountName"] storage account"
    set -e
    (
        # create deadletter container
        az storage container create \
        --name $deadLetterContainerName \
        --public-access off \
        --connection-string $connectionString 1> /dev/null
    )
    echo "["$deadLetterContainerName"] container successfully created in ["$storageAccountName"] storage account"
else
	echo "A container called ["$deadLetterContainerName"] already exists in ["$storageAccountName"] storage account"
fi

# checking if files container exists
echo "Checking if ["$filesContainerName"] container already exists..."
set +e
(
    az storage container show --name $filesContainerName --connection-string $connectionString &> /dev/null
)

if [ $? != 0 ]; then
	echo "No ["$filesContainerName"] container actually exists in ["$storageAccountName"] storage account"
    set -e
    (
        # create files container
        az storage container create \
        --name $filesContainerName \
        --public-access off \
        --connection-string $connectionString 1> /dev/null
    )
    echo "["$filesContainerName"] container successfully created in ["$storageAccountName"] storage account"
else
	echo "A container called ["$filesContainerName"] already exists in ["$storageAccountName"] storage account"
fi

# retrieve resource id for the storage account
echo "Retrieving the resource id for ["$storageAccountName"] storage account..."
storageAccountId=$(az storage account show --name $storageAccountName --resource-group $storageAccountResourceGroup --query id --output tsv 2> /dev/null)

if [ -n $storageAccountId ]; then
    echo "Resource id for ["$storageAccountName"] storage account successfully retrieved: ["$storageAccountId"]"
else
    echo "Failed to retrieve resource id for ["$storageAccountName"] storage account"
    return
fi

echo "Checking if Azure CLI eventgrid extension is installed..."
set +e
(
    az extension show --name eventgrid --query name --output tsv &> /dev/null
)

if [ $? != 0 ]; then
	echo "The Azure CLI eventgrid extension was not found. Installing the extension..."
  az extension add --name eventgrid
else
    echo "Azure CLI eventgrid extension successfully found. Updating the extension to the latest version..."
  az extension update --name eventgrid
fi

# checking if the subscription already exists
echo "Checking if ["$subscriptionName"] Event Grid subscription already exists for ["$storageAccountName"] storage account..."
set +e
(
    az eventgrid event-subscription show --name $subscriptionName --source-resource-id $storageAccountId &> /dev/null
)

if [ $? != 0 ]; then
	echo "No ["$subscriptionName"] Event Grid subscription actually exists for ["$storageAccountName"] storage account"
    echo "Creating a subscription for the ["$endpointUrl"] ngrock local endpoint..."

	set +e
	(
		az eventgrid event-subscription create \
        --source-resource-id $storageAccountId \
        --name $subscriptionName \
        --endpoint-type webhook \
        --endpoint $endpointUrl \
        --subject-begins-with $subjectBeginsWith \
        --deadletter-endpoint $storageAccountId/blobServices/default/containers/$deadLetterContainerName 1> /dev/null
	)

    if [ $? == 0 ]; then
        echo "["$subscriptionName"] Event Grid subscription successfully created"
    fi
else
	echo "An Event Grid subscription called ["$subscriptionName"] already exists for ["$storageAccountName"] storage account"
fi