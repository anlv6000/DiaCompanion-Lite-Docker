# Bộ kiểm thử API end-to-end. Chỉ cần requests + python-docx (sinh báo cáo Word).
FROM python:3.11-slim
ENV PIP_DISABLE_PIP_VERSION_CHECK=1 PYTHONUNBUFFERED=1 PIP_DEFAULT_TIMEOUT=120 PIP_RETRIES=10
RUN pip install --no-cache-dir requests==2.32.3 python-docx==1.1.2
WORKDIR /tests
COPY docker/test/run_tests.py docker/test/report_docx.py docker/test/cases.py /tests/
COPY docker/test/fixtures/ /tests/fixtures/
ENTRYPOINT ["python", "/tests/run_tests.py"]
