FROM python:3.12-slim AS app
# ffmpeg: yt-dlp puts a YouTube video's picture and sound together into one mp4
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir --root-user-action=ignore -r requirements.txt
COPY app/ ./
EXPOSE 8080
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8080"]

# the tests: docker build --target test -t audio-api-test . ; run with AUDIO_DATABASE_URL set
FROM app AS test
COPY requirements-test.txt .
RUN pip install --no-cache-dir --root-user-action=ignore -r requirements-test.txt
COPY tests/ ./tests/
CMD ["python", "-m", "pytest", "-q", "tests"]

FROM app
