# Azure OpenAI Sora Video Generation Test

This Python script tests video generation using Azure OpenAI's Sora model.

## Azure Portal Setup

### 1. Create Azure OpenAI Resource
1. Go to [Azure Portal](https://portal.azure.com)
2. Click "Create a resource" → Search for "Azure OpenAI"
3. Click "Create" and fill in:
   - **Subscription**: Your Azure subscription
   - **Resource Group**: Create new or select existing
   - **Region**: Choose a supported region (e.g., East US, West Europe)
   - **Name**: Your resource name (e.g., "my-sora-openai")
   - **Pricing Tier**: Standard S0
4. Click "Review + Create" → "Create"

### 2. Deploy Sora Model
1. Navigate to your Azure OpenAI resource
2. Go to "Model deployments" in the left sidebar
3. Click "Create new deployment"
4. Select:
   - **Model**: `sora` (if available in preview)
   - **Deployment name**: `sora` 
   - **Version**: Latest available
5. Click "Create"

### 3. Get Credentials
1. In your Azure OpenAI resource, go to "Keys and Endpoint"
2. Copy:
   - **Endpoint**: Something like `https://your-resource.openai.azure.com/`
   - **Key 1**: Your API key

### 4. Request Access (if needed)
- Sora may be in limited preview
- Submit access request at [Azure OpenAI Access](https://customervoice.microsoft.com/Pages/ResponsePage.aspx?id=v4j5cvGGr0GRqy180BHbR7en2Ais5pxKtso_Pz4b1_xUNTZBNzRKNlVQSFhZMU9aV09EVzYxWFdORCQlQCN0PWcu)
- Wait for approval email before proceeding

## Setup

1. Install dependencies:
```cmd
pip install -r requirements.txt
```

2. Configure environment:
```cmd
copy .env.example .env
# Edit .env with your Azure OpenAI credentials from step 3 above
```

3. Run the test:
```cmd
python test_sora_video.py
```

## Configuration

- `AZURE_OPENAI_ENDPOINT`: Your Azure OpenAI endpoint URL
- `AZURE_OPENAI_API_KEY`: Your Azure OpenAI API key

## Features

- Creates video generation jobs
- Polls for completion status
- Downloads generated videos
- Supports multiple resolutions (480x480 to 1920x1080)
- Configurable video duration (1-20 seconds)
- Error handling and timeout protection

## Output

Generated videos are saved as `test_video.mp4` by default.