#!/usr/bin/env python3
"""
Azure OpenAI Sora Video Generation Test Script

This script demonstrates how to generate videos using Azure OpenAI's Sora model.
Based on Microsoft's official documentation and examples.

Requirements:
- Azure OpenAI resource with Sora model deployed
- Python 3.8+
- Environment variables: AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY

Usage:
python test_sora_video.py
"""

import os
import sys
import time
import json
import requests
from typing import Optional, Dict, Any
from urllib.parse import urljoin
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()


class SoraVideoGenerator:
    """Azure OpenAI Sora video generation client."""
    
    def __init__(self, endpoint: str, api_key: str, api_version: str = "preview"):
        """Initialize the Sora client.
        
        Args:
            endpoint: Azure OpenAI endpoint URL
            api_key: Azure OpenAI API key
            api_version: API version to use
        """
        self.endpoint = endpoint.rstrip('/')
        self.api_key = api_key
        self.api_version = api_version
        self.headers = {
            "api-key": api_key,
            "Content-Type": "application/json"
        }
    
    def create_video_job(self, prompt: str, width: int = 480, height: int = 480, 
                        n_seconds: int = 5, model: str = "sora") -> Optional[str]:
        """Create a video generation job.
        
        Args:
            prompt: Text description of the video to generate
            width: Video width in pixels (480-1920)
            height: Video height in pixels (480-1920)
            n_seconds: Video duration in seconds (1-20)
            model: Model name (default: "sora")
            
        Returns:
            Job ID if successful, None otherwise
        """
        url = f"{self.endpoint}/openai/v1/video/generations/jobs?api-version={self.api_version}"
        
        body = {
            "prompt": prompt,
            "width": width,
            "height": height,
            "n_seconds": n_seconds,
            "model": model
        }
        
        print(f"Creating video generation job...")
        print(f"Prompt: {prompt}")
        print(f"Resolution: {width}x{height}")
        print(f"Duration: {n_seconds} seconds")
        
        try:
            response = requests.post(url, headers=self.headers, json=body)
            response.raise_for_status()
            
            job_data = response.json()
            job_id = job_data.get("id")
            
            if job_id:
                print(f"✅ Job created successfully: {job_id}")
                return job_id
            else:
                print(f"❌ No job ID in response: {job_data}")
                return None
                
        except requests.exceptions.RequestException as e:
            print(f"❌ Error creating job: {e}")
            if hasattr(e.response, 'text'):
                print(f"Response: {e.response.text}")
            return None
    
    def get_job_status(self, job_id: str) -> Optional[Dict[str, Any]]:
        """Get the status of a video generation job.
        
        Args:
            job_id: Job ID to check
            
        Returns:
            Job status data if successful, None otherwise
        """
        url = f"{self.endpoint}/openai/v1/video/generations/jobs/{job_id}?api-version={self.api_version}"
        
        try:
            response = requests.get(url, headers=self.headers)
            response.raise_for_status()
            return response.json()
            
        except requests.exceptions.RequestException as e:
            print(f"❌ Error getting job status: {e}")
            return None
    
    def download_video(self, generation_id: str, output_path: str) -> bool:
        """Download the generated video.
        
        Args:
            generation_id: Generation ID from job status
            output_path: Local file path to save the video
            
        Returns:
            True if successful, False otherwise
        """
        url = f"{self.endpoint}/openai/v1/video/generations/{generation_id}/content/video?api-version={self.api_version}"
        
        try:
            response = requests.get(url, headers=self.headers, stream=True)
            response.raise_for_status()
            
            with open(output_path, 'wb') as f:
                for chunk in response.iter_content(chunk_size=8192):
                    f.write(chunk)
            
            print(f"✅ Video saved to: {output_path}")
            return True
            
        except requests.exceptions.RequestException as e:
            print(f"❌ Error downloading video: {e}")
            return False
    
    def generate_video(self, prompt: str, width: int = 480, height: int = 480, 
                      n_seconds: int = 5, output_path: str = "generated_video.mp4",
                      max_wait_time: int = 300) -> bool:
        """Generate a video and download it.
        
        Args:
            prompt: Text description of the video
            width: Video width in pixels
            height: Video height in pixels  
            n_seconds: Video duration in seconds
            output_path: Where to save the generated video
            max_wait_time: Maximum time to wait for generation (seconds)
            
        Returns:
            True if successful, False otherwise
        """
        # Create job
        job_id = self.create_video_job(prompt, width, height, n_seconds)
        if not job_id:
            return False
        
        # Poll for completion
        start_time = time.time()
        print(f"Waiting for video generation to complete...")
        
        while time.time() - start_time < max_wait_time:
            status_data = self.get_job_status(job_id)
            if not status_data:
                print("❌ Failed to get job status")
                return False
            
            status = status_data.get("status")
            print(f"Status: {status}")
            
            if status == "succeeded":
                # Get generation ID and download video
                generations = status_data.get("generations", [])
                if generations:
                    generation_id = generations[0].get("id")
                    if generation_id:
                        return self.download_video(generation_id, output_path)
                    else:
                        print("❌ No generation ID found")
                        return False
                else:
                    print("❌ No generations found")
                    return False
            
            elif status == "failed":
                error = status_data.get("error", {})
                print(f"❌ Job failed: {error}")
                return False
            
            elif status in ["running", "pending", "preprocessing", "processing"]:
                # Still processing, wait and retry
                time.sleep(5)
                continue
            
            else:
                print(f"❌ Unknown status: {status}")
                return False
        
        print(f"❌ Timeout after {max_wait_time} seconds")
        return False


def main():
    """Main function to test Sora video generation."""
    
    # Get configuration from environment variables
    endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
    api_key = os.getenv("AZURE_OPENAI_API_KEY")
    
    if not endpoint or not api_key:
        print("❌ Missing required environment variables:")
        print("   AZURE_OPENAI_ENDPOINT - Your Azure OpenAI endpoint URL")
        print("   AZURE_OPENAI_API_KEY - Your Azure OpenAI API key")
        sys.exit(1)
    
    # Initialize client
    client = SoraVideoGenerator(endpoint, api_key)
    
    # Test video generation
    prompt = "A happy golden retriever playing fetch in a sunny park, slow motion"
    
    print(f"🎬 Testing Azure OpenAI Sora Video Generation")
    print(f"Endpoint: {endpoint}")
    print(f"=" * 60)
    
    success = client.generate_video(
        prompt=prompt,
        width=480,
        height=480,
        n_seconds=5,
        output_path="test_video.mp4"
    )
    
    if success:
        print("🎉 Video generation completed successfully!")
        print("Check test_video.mp4 for the generated video.")
    else:
        print("💥 Video generation failed!")
        sys.exit(1)


if __name__ == "__main__":
    main()