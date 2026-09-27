FROM python:3.13-slim
WORKDIR /app
COPY pyproject.toml uv.lock ./
RUN pip install --no-cache-dir uv && uv sync --frozen --no-dev
COPY server/ ./server/
RUN useradd --create-home game
USER game
EXPOSE 8000
CMD ["/app/.venv/bin/fastapi", "run", "--host", "0.0.0.0", "--port", "8000"]
