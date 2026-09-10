#!/usr/bin/env python3
"""Verify the PDF file and print its size and header"""

import os

pdf_path = os.path.join(os.path.dirname(__file__), '..', 'docs', 'R1-Report.pdf')

if os.path.exists(pdf_path):
    with open(pdf_path, 'rb') as f:
        data = f.read()
        size = len(data)
        header = data[:8]
        print(f"PDF File: {pdf_path}")
        print(f"Size: {size} bytes ({size / 1024:.2f} KB)")
        print(f"Header: {header}")
        print(f"Valid PDF: {data.startswith(b'%PDF-')}")
else:
    print(f"PDF file not found: {pdf_path}")
