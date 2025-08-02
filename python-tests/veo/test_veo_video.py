import os
from dotenv import load_dotenv
from google import genai
import time
from google.genai import types

load_dotenv()

# read env variable
GOOGLE_API_KEY = os.getenv("GOOGLE_API_KEY")

client = genai.Client(api_key=GOOGLE_API_KEY)

VEO_MODEL_ID = "veo-2.0-generate-001" # @param ["veo-2.0-generate-001"] {"allow-input":true, isTemplate: true}

prompt = "A neon hologram of a cat driving at top speed" # @param {type: "string"}

# Here are a few prompts to help you get started and spark your creativity:
# 1. Wide shot of a futuristic cityscape at dawn. Flying vehicles zip between skyscrapers. Camera pans across the skyline as the sun rises.
# 2. A close up of a thief's gloved hand that reaches for a priceless diamond necklace in a museum display case. Camera slowly tracks the hand, with dramatic lighting and shadows.
# 3. A giant, friendly robot strolls through a field of wildflowers, butterflies fluttering around its head. Camera tilts upwards as the robot looks towards the sky.
# 4. A single, perfectly ripe apple hangs from a branch. It is covered in dew. A gentle breeze sways the branch, causing the apple to rotate slowly.
# 5. A beehive nestled in a hollow tree trunk in a magical forest. Bees fly in and out of the hive, carrying pollen and nectar
# 6. In a beautiful field of flowers, show a cute bunny that is slowly revealed to be an eldritch horror from outside time and space.

# Optional parameters
negative_prompt = "" # @param {type: "string"}
person_generation = "allow_adult"  # @param ["dont_allow", "allow_adult"]
aspect_ratio = "16:9" # @param ["16:9", "9:16"]
number_of_videos = 1 # @param {type:"slider", min:1, max:4, step:1}
duration = 8 # @param {type:"slider", min:5, max:8, step:1}

operation = client.models.generate_videos(
    model=VEO_MODEL_ID,
    prompt=prompt,
    config=types.GenerateVideosConfig(
    # At the moment the config must not be empty
    person_generation=person_generation,
    aspect_ratio=aspect_ratio,  # 16:9 or 9:16
    number_of_videos=number_of_videos, # supported value is 1-4
    negative_prompt=negative_prompt,
    duration_seconds=duration, # supported value is 5-8
    ),
)

# Waiting for the video(s) to be generated
while not operation.done:
    time.sleep(20)
    operation = client.operations.get(operation)
    print(operation)

print(operation.result.generated_videos)

for n, generated_video in enumerate(operation.result.generated_videos):
    client.files.download(file=generated_video.video)
    generated_video.video.save(f'video{n}.mp4') # Saves the video(s)