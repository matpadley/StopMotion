#!/bin/bash

# Test script for the Image Slideshow Generator
# This script creates some sample images and tests the slideshow generation

echo "Creating test images..."

# Create a test directory
mkdir -p test_images

# Create some sample colored images using ImageMagick (if available)
# If ImageMagick is not available, this will fail gracefully
if command -v convert &> /dev/null; then
    echo "Creating sample images with ImageMagick..."
    
    # Create different sized colored rectangles
    convert -size 800x600 xc:red test_images/red_rectangle.png
    convert -size 1200x800 xc:blue test_images/blue_rectangle.jpg
    convert -size 600x400 xc:green test_images/green_rectangle.jpeg
    convert -size 1000x1000 xc:yellow test_images/yellow_square.png
    
    echo "Sample images created in test_images directory"
    echo "Running slideshow generator..."
    
    # Run the slideshow generator
    cd ImageConcat
    dotnet run test_images
    
    echo "Check for the generated MP4 file in the test_images directory!"
else
    echo "ImageMagick not found. Please install it with: brew install imagemagick"
    echo "Or manually add some image files to the test_images directory and run:"
    echo "dotnet run test_images"
fi
