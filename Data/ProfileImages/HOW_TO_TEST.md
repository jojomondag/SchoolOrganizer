# How to Test the Image Selector

## Quick Test:
1. Run the application (dotnet run)
2. Click "Add Student" button 
3. The Image Selector window should open
4. You should now see test images in the gallery
5. Click on an image to see the preview
6. Use the red X button to remove images

## Adding Your Own Images:
1. Copy any JPG, PNG, or other image files to this folder:
   `Data/ProfileImages/`

2. Supported formats:
   - .jpg, .jpeg
   - .png  
   - .bmp
   - .gif
   - .webp

3. The Image Selector will automatically detect and display them

## Features to Test:
- ✅ Image gallery display
- ✅ Large preview panel  
- ✅ Remove image functionality (red X button)
- ✅ Browse for new images
- ✅ Image details display
- ✅ Selection confirmation

## Troubleshooting:
- If images don't appear, check they are valid image files
- Make sure files are in the correct folder
- Check the debug output for error messages
- Try refreshing by reopening the image selector