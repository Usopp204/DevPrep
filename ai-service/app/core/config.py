from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict


ROOT_ENV_PATH = Path(__file__).resolve().parents[3] / ".env"


class Settings(BaseSettings):
    app_name: str = "DevPrep AI Service"
    host: str = "0.0.0.0"
    port: int = 8000

    model_config = SettingsConfigDict(
        env_file=str(ROOT_ENV_PATH),
        env_prefix="AI_",
        extra="ignore",
    )


settings = Settings()
