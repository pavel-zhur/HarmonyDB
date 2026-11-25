"""Errors: every refusal is an HTTP status with a stable code; the message is for developers."""
from __future__ import annotations

from pydantic import BaseModel


class Problem(BaseModel):
    code: str
    message: str


class ApiError(Exception):
    def __init__(self, status: int, code: str, message: str, headers: dict | None = None):
        super().__init__(message)
        self.status, self.code, self.message, self.headers = status, code, message, headers


# The codes, in one place (the README lists them too).
KEY_MISSING = "key_missing"                         # 401: no X-Internal-Key
KEY_INVALID = "key_invalid"                         # 401: the key is not a configured client's
SCOPE_MISSING = "scope_missing"                     # 403: the client lacks the endpoint's scope
MODE_INVALID = "mode_invalid"                       # 400: X-Audio-Mode missing, or not one the endpoint takes
SUBJECT_MISSING = "subject_missing"                 # 400: person mode without X-Audio-Subject
CREDENTIALS_MISSING = "credentials_missing"         # 400: person mode without X-Audio-Credentials
CREDENTIALS_INVALID = "credentials_invalid"         # 400: credentials that cannot be read
CREDENTIALS_REJECTED = "credentials_rejected"       # 403: the service did not accept the person's credentials
PERSON_REQUIRED = "person_required"                 # 400: a person's own list asked in another mode
SERVICE_REQUIRED = "service_required"               # 400: an operation only the service account may do
SERVICE_NOT_ALLOWED = "service_not_allowed"         # 400: a playlist asked as the service account (it is asked anonymously or as a person)
ACCOUNT_REQUIRED = "account_required"               # 403: the service gives this to an account only (asked without one)
SERVICE_ACCOUNT_MISSING = "service_account_missing"     # 503: the service account is not configured
SERVICE_ACCOUNT_REJECTED = "service_account_rejected"   # 503: the service did not accept the service account
BAD_REQUEST = "bad_request"                         # 400: a parameter is wrong
LINK_UNKNOWN = "link_unknown"                       # 400: not a link of either service, or not understood
LINK_NOT_PLAYLIST = "link_not_playlist"             # 400: a link that names something else than a playlist
HOST_NOT_ALLOWED = "host_not_allowed"               # 400: a file address outside the services' hosts
NOT_FOUND = "not_found"                             # 404: no such thing (on the service, or in the store)
NO_FILE = "no_file"                                 # 404: the service gives no file for this track
TOO_LARGE = "too_large"                             # 413: a file over the configured limit
RANGE_NOT_SATISFIABLE = "range_not_satisfiable"     # 416
UPSTREAM_FAILED = "upstream_failed"                 # 502: the music service failed or refused
INTERNAL_ERROR = "internal_error"                   # 500
